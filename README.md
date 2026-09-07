# AutoRepairShop 🛠️

Projeto backend desenvolvido como **Tech Challenge**, simulando o sistema de gestão de uma oficina mecânica, com foco em **Ordem de Serviço (Service Order)**, domínio rico e boas práticas de arquitetura.

--- 

## 📐 Arquitetura

O projeto segue uma **Clean Architecture simplificada**, com princípios de **DDD (Domain-Driven Design)** e separação clara de responsabilidades:


```
AutoRepairShop
├── src
│ ├── AutoRepairShop.Api # Camada de apresentação (HTTP / Controllers)
│ ├── AutoRepairShop.Application # Application Services (casos de uso)
│ ├── AutoRepairShop.Domain # Domínio (Entidades, VOs, Enums, Regras)
│ └── AutoRepairShop.Infrastructure # Infraestrutura (EF Core, Repositórios, Migrations)
│
├── infra # Scripts Terraform
|
├── k8s # Manifestos Kubernets
|
├── tests
│ └── AutoRepairShop.Tests # Testes unitários e de integração
│
├── docker-compose.yml # Orquestração API + SQL Server
├── .env.example # Exemplo de variáveis de ambiente
└── AutoRepairShop.sln
```

### Padrões adotados
- Domain-Driven Design (DDD)
- Aggregate Root (`ServiceOrder`)
- Value Objects
- Repositórios
- DTOs + Mappers
- Entity Framework Core
- Separação por camadas

---

## Banco de dados

A escolha por um banco de dados relacional para o sistema AutoRepairShop foi motivada pelas características do domínio e pelos requisitos de consistência, integridade e rastreabilidade das informações.
O domínio do sistema possui relacionamentos bem definidos e fortemente estruturados, como:
- Clientes e seus veículos;
- Ordens de serviço associadas a clientes, veículos e serviços;
- Controle de status e histórico de execução das ordens.
  
Essas relações possuem regras de negócio claras, dependências entre entidades e necessidade de integridade referencial, o que torna o modelo relacional uma escolha natural e adequada.

---

## 🧩 Principais Funcionalidades

### Ordem de Serviço (Service Order)
- Criação de Ordem de Serviço
- Associação de cliente e veículo
- Inclusão de serviços
- Inclusão de peças/insumos (supplies)
- Geração automática de orçamento
- Fluxo de status:
  - Received
  - InDiagnosis
  - WaitingApproval
  - InExecution
  - Finished
  - Delivered
  - Canceled
- Consulta de OS por cliente
- Monitoramento de tempo médio de execução

---

## 🐳 Executando com Docker

### Pré-requisitos
- Docker Desktop
- .NET SDK 8+

---

### 1️⃣ Configurar variáveis de ambiente

Crie um arquivo `.env` na raiz do projeto (não versionado):

```env
DB_PASSWORD=your_strong_password_here

JWT_KEY=your_super_secret_jwt_key_with_256_bits_or_more
JWT_ISSUER=AutoRepairShop
JWT_AUDIENCE=AutoRepairShopUsers
JWT_EXPIRES_IN_MINUTES=15
```
---

### 2️⃣ Subir API + Banco de Dados

Na raiz do projeto:

```bash
docker compose up -d --build
```

Isso irá subir:
- **SQL Server 2022** (container)
- **API AutoRepairShop**

---

🔌 String de Conexão

A aplicação suporta execução fora do Docker, enquanto o banco roda em container.

Execução local da API (banco no Docker)

````json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=AutoRepairShopDb;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True"
}
````

Execução da API dentro do Docker

````json
"ConnectionStrings": {
  "DefaultConnection": "Server=sqlserver,1433;Database=AutoRepairShopDb;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True"
}
````

⚠️ sqlserver é o nome do serviço definido no docker-compose.yml.

## 🗄️ Migrations (EF Core)

As migrations ficam no projeto:

```
src/AutoRepairShop.Infrastructure
```

### Criar uma migration

```bash
dotnet ef migrations add InitialCreate \
  --project src/AutoRepairShop.Infrastructure \
  --startup-project src/AutoRepairShop.Api
```

### Aplicar migrations

```bash
dotnet ef database update \
  --project src/AutoRepairShop.Infrastructure \
  --startup-project src/AutoRepairShop.Api
```

> 💡 O banco precisa estar rodando (via Docker)

---

## 🔐 Autenticação JWT

A aplicação utiliza JWT (JSON Web Token) para autenticação.

Configuração segura

Nenhuma secret é versionada.
As configurações são lidas via variáveis de ambiente.

````json
"Jwt": {
  "Key": "${JWT_KEY}",
  "Issuer": "${JWT_ISSUER}",
  "Audience": "${JWT_AUDIENCE}",
  "ExpiresInMinutes": "${JWT_EXPIRES_IN_MINUTES}"
}
````

Variáveis injetadas no Docker

---
## 🧪 Testes

### Tipos de testes implementados
- Testes unitários do domínio (entidades e regras)
- Testes de Application Services
- Testes de integração com banco

### Executar testes

```bash
dotnet test
```

### Cobertura
- Cobertura mínima exigida: **80% nos domínios críticos**
- Foco em `ServiceOrder` e fluxos principais

---

# 🚀 Infraestrutura com Terraform e Kubernetes

Este repositório demonstra a **provisão de infraestrutura com Terraform** e o **deploy de uma aplicação em Kubernetes**, seguindo boas práticas de **Infrastructure as Code (IaC)**, versionamento e reprodutibilidade.

<img width="1536" height="1024" alt="image" src="https://github.com/user-attachments/assets/bbe1b586-03eb-4330-8052-13470df8ebda" />


---

## 📌 Objetivo do Projeto

- Demonstrar o uso do **Terraform** para criar e gerenciar infraestrutura
- Orquestrar aplicações usando **Kubernetes**
---
## ⚙️ Pré-requisitos

Instale as seguintes ferramentas em sua máquina:

- **Terraform**
- **kubectl**
- **Minikube**

### ✅ Validação das instalações

Execute os comandos abaixo para confirmar que tudo está instalado corretamente:

```bash
terraform -v
kubectl version --client
minikube version
```
---

## ▶️ Como Executar o Projeto
### 1️⃣ Iniciar o cluster Kubernetes local

O cluster local é provisionado via Minikube:


```bash 
minikube start
```

### 2️⃣ Inicializar o Terraform

Acesse a pasta de infraestrutura e inicialize o Terraform:

```bash 
cd infra
terraform init
```

Este comando irá:

- Baixar os providers necessários 
- Inicializar o backend de estado 
- Preparar o ambiente para execução dos planos 

### 3️⃣ Validar e testar a infraestrutura
Validação da sintaxe e configuração

```bash 
terraform validate
```
Simulação das mudanças (dry-run)

```bash 
terraform plan
```
Neste passo é possível demonstrar claramente:

- Quais recursos serão criados
- Quais serão alterados
- Quais seriam removidos, se aplicável

Este comando não altera o ambiente, apenas simula o impacto do apply.

### 4️⃣ Aplicar a infraestrutura

Para provisionar os recursos definidos:

```bash 
terraform apply
```

Confirme a execução digitando yes quando solicitado.

Este passo cria:

- Recursos de infraestrutura definidos via Terraform
- Base necessária para execução da aplicação no Kubernetes
- Aplica os manifestos da aplicação

Os manifests incluem:

- Deployments
- Services
- ConfigMaps
- Secrets
- Horizontal Pod Autoscaler (HPA)

### 5️⃣ Verificar os recursos criados

Comandos para validação do ambiente:

```bash 
kubectl get pods -n oficina
kubectl get svc -n oficina
kubectl get deploy -n oficina
```

## 📦 Tecnologias Utilizadas

- .NET 8
- ASP.NET Core
- Entity Framework Core
- SQL Server 2022
- Docker & Docker Compose
- xUnit / FluentAssertions / Moq
- Terraform
- Kubernets

---


## 🚀 Status do Projeto

✔ Estrutura definida
✔ Domínio modelado
✔ Docker configurado
✔ Migrations funcionando
✔ Aggregate implementado
✔ Endpoints REST
✔ Testes finais
✔ Infraestrutura escalável
✔ Deploy automatizado

---

## 👩‍💻 Autores

Projeto desenvolvido por **Dhiulia da Silva e Mateus Pinheiro** como parte de avaliação técnica.

#### [Swagger.json do projeto](https://github.com/dhiuliaantunes/AutoRepairShop/blob/master/src/AutoRepairShop.Api/swagger.json)
---
