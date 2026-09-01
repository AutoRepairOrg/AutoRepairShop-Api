# RFCs - Request for Comments

Decisões técnicas relevantes que foram discutidas e aprovadas para o projeto AutoRepairShop.

---

## RFC-001: Escolha da Nuvem AWS

**Status:** APROVADO  
**Data:** 2026-08-01  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos escolher um provedor de nuvem para hospedar toda a infraestrutura do sistema AutoRepairShop (banco de dados, Kubernetes, serverless, API Gateway).

### Opções Consideradas

#### 1. AWS (Amazon Web Services)
**Prós:**
- Maior marketshare (32% do mercado)
- Serviços maduros (EKS, RDS, Lambda desde 2014)
- Melhor integração entre serviços
- Documentação extensa
- AWS Academy/Educate para aprendizado
- Free tier generoso (750h EC2, 1M Lambda requests, 20GB RDS)

**Contras:**
- Curva de aprendizado inicial
- Custos podem crescer rapidamente sem governança

#### 2. Microsoft Azure
**Prós:**
- Integração nativa com .NET e Visual Studio
- Azure DevOps robusto
- Active Directory integrado
- Suporte corporativo forte

**Contras:**
- Menor oferta de free tier
- Menos familiaridade da equipe
- Kubernetes (AKS) menos maduro que EKS
- Custos de licenciamento Windows

#### 3. Google Cloud Platform (GCP)
**Prós:**
- GKE (Kubernetes nativo do Google)
- BigQuery para analytics
- Preços competitivos
- Machine Learning integrado

**Contras:**
- Menor ecossistema que AWS
- Menos adoção no mercado brasileiro
- Pouca experiência da equipe
- Documentação menos abundante em português

### Decisão

**Escolhido:** AWS (Amazon Web Services)

### Justificativa

1. **Experiência prévia:** Equipe já trabalhou com AWS em projetos anteriores
2. **Free Tier:** 750h/mês de EC2, 1M requests Lambda, 20GB RDS - suficiente para MVP
3. **Maturidade dos serviços:** EKS (2018), RDS (2009) e Lambda (2014) são serviços consolidados
4. **Comunidade:** Maior volume de conteúdo educacional, Stack Overflow e troubleshooting
5. **Requisitos do projeto:** Tech Challenge aceita qualquer cloud pública, AWS atende todos os requisitos técnicos

### Consequências

**Positivas:**
- Acesso a serviços maduros e bem documentados
- Free tier permite desenvolvimento sem custos iniciais
- Maior facilidade em encontrar soluções para problemas

**Negativas:**
- Equipe precisa aprofundar conhecimento em IAM, VPC, Security Groups
- Custos estimados após free tier: $105-158/mês
- Vendor lock-in parcial (mitigado pelo uso de Terraform)

### Métricas de Sucesso

- Infraestrutura provisionada em menos de 2 horas via Terraform
- Uptime > 99.5% nos primeiros 3 meses
- Custo mensal < $200

### Alternativas Futuras

- Multi-cloud usando Terraform para infraestrutura replicável
- Migração para Azure se houver necessidade de integração AD corporativa
- Kubernetes gerenciado on-premises se custos cloud ficarem proibitivos

---

## RFC-002: Escolha do Banco de Dados - SQL Server

**Status:** APROVADO  
**Data:** 2026-08-05  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos escolher um banco de dados gerenciado para armazenar dados da oficina (clientes, veículos, ordens de serviço, administradores). O sistema já possui migrations escritas em Entity Framework Core.

### Opções Consideradas

#### 1. PostgreSQL (RDS)
**Prós:**
- Open source (sem custos de licença)
- Performance excelente para reads e writes
- Suporte JSON nativo (JSONB)
- Comunidade ativa e crescente
- Extensões avançadas (PostGIS, pgvector)
- Custo menor (db.t3.micro ~$12/mês)

**Contras:**
- Equipe menos familiarizada com PostgreSQL
- Requer reescrita completa das migrations
- Tooling menos integrado com .NET que SQL Server
- Necessidade de aprender PL/pgSQL ao invés de T-SQL

#### 2. MySQL (RDS)
**Prós:**
- Open source e amplamente adotado
- Leve e rápido para aplicações web
- Ótima performance para leitura
- Custo baixo (db.t3.micro ~$12/mês)
- Compatibilidade com MariaDB

**Contras:**
- Menos features avançadas que PostgreSQL/SQL Server
- Entity Framework Core tem suporte inferior
- Migrations precisariam ser reescritas
- Menos recursos de segurança nativos

#### 3. SQL Server Express (RDS)
**Prós:**
- **Migrations existentes 100% compatíveis**
- Entity Framework Core otimizado para SQL Server
- T-SQL familiar para a equipe
- Ferramentas robustas (SSMS, Azure Data Studio)
- RDS gerencia backups, patches e multi-AZ
- Transações ACID completas
- Security row-level nativo

**Contras:**
- Licença proprietária Microsoft (mitigado pela Express Edition)
- Limitação de 10GB por database (suficiente para MVP)
- Custo ligeiramente maior (~$15/mês para db.t3.micro)
- Vendor lock-in Microsoft

### Decisão

**Escolhido:** SQL Server Express (RDS)

### Justificativa

1. **Compatibilidade total:** 100% das migrations EF Core funcionam sem modificação
2. **Produtividade:** Equipe domina T-SQL, SSMS e debugging de queries
3. **Integração .NET:** Entity Framework Core tem melhor performance e features com SQL Server
4. **Custo-benefício aceitável:** RDS Express ~$15/mês está dentro do orçamento
5. **Gerenciamento automatizado:** RDS cuida de backups (7 dias), patches de segurança, monitoring
6. **Time-to-market:** Zero retrabalho permite foco em features de negócio

### Consequências

**Positivas:**
- Zero retrabalho nas migrations (economia de 8-16h de desenvolvimento)
- Equipe trabalha com stack conhecida
- Ferramentas de debugging familiares

**Negativas:**
- Limitação de 10GB por database (monitorar crescimento trimestral)
- Custo 25% maior que PostgreSQL
- Dependência de tecnologia Microsoft

### Plano de Monitoramento

- Alertas CloudWatch quando storage > 70% (7GB)
- Revisão trimestral de custos
- Benchmark de performance (latência < 50ms para 95% das queries)

### Plano de Migração Futura (se necessário)

**Para PostgreSQL:**
1. Usar AWS Schema Conversion Tool (SCT)
2. Reescrever migrations EF Core
3. Testar compatibilidade de queries
4. Migração blue-green com AWS DMS

**Para MySQL:**
1. Reescrever migrations
2. Validar tipos de dados compatíveis
3. Testar performance com volume real

**Para NoSQL (DynamoDB/MongoDB):**
1. Avaliar se modelo relacional ainda é adequado
2. Considerar para features específicas (caching, analytics)

### Métricas de Sucesso

- Backups automáticos rodando diariamente (03:00 UTC)
- Latência p95 < 50ms para queries simples
- Uptime > 99.5%
- Custo mensal < $20

---

## RFC-003: Estratégia de Autenticação - Lambda + JWT

**Status:** APROVADO  
**Data:** 2026-08-10  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos implementar autenticação segura para proteger as APIs do sistema. O Tech Challenge exige **Function Serverless para autenticação** que valide CPF, consulte o banco e gere JWT.

### Requisitos Obrigatórios (Tech Challenge)

1. Function Serverless para autenticação
2. Validação de CPF do cliente
3. Consulta à base de dados para verificar existência e status
4. Geração de token JWT válido para consumo das APIs protegidas

### Opções Consideradas

#### 1. AWS Cognito
**Prós:**
- Gerenciado totalmente pela AWS
- OAuth 2.0 / OpenID Connect nativo
- MFA (Multi-Factor Authentication) integrado
- User pools para gerenciar usuários
- Federação com Google, Facebook, SAML
- Custo: $0.0055 por MAU (Monthly Active User)

**Contras:**
- **NÃO ATENDE REQUISITO:** Tech Challenge exige Lambda customizado
- Não permite validação customizada de CPF
- Impossível consultar banco próprio antes de autenticar
- Menos flexibilidade para lógica de negócio
- Curva de aprendizado do Cognito User Pools

#### 2. Lambda + JWT (Customizado)
**Prós:**
- **ATENDE REQUISITO:** Function Serverless customizada
- Controle total da lógica de autenticação
- Validação de CPF integrada no código
- Consulta direta ao RDS SQL Server
- JWT padrão da indústria (RFC 7519)
- Custo baixo: ~$0.20 para 1M requests
- Integração com API Gateway Authorizer

**Contras:**
- Precisa implementar validação JWT manualmente
- Gerenciar chave secreta (mitigado com Secrets Manager)
- Implementar refresh tokens manualmente
- Responsabilidade por segurança do token

#### 3. API Gateway + Auth0 (SaaS)
**Prós:**
- SaaS completo de autenticação
- Dashboard de gerenciamento visual
- Social login (Google, Facebook, Apple)
- MFA, anomaly detection, breach detection
- SDKs para múltiplas linguagens

**Contras:**
- **NÃO ATENDE REQUISITO:** Tech Challenge exige Lambda
- Custo elevado: $25/mês base + $35 por 1000 MAU
- Vendor lock-in severo (difícil migrar depois)
- Não permite validação de CPF customizada
- Impossível consultar RDS próprio

#### 4. API em .NET + Identity Framework
**Prós:**
- Framework maduro do .NET
- Integração com Entity Framework
- Password hashing, claims, roles nativos

**Contras:**
- **NÃO ATENDE REQUISITO:** Não é serverless
- Acoplado à aplicação principal
- Menos escalável que Lambda
- Custo de infra sempre rodando

### Decisão

**Escolhido:** Lambda + JWT (Customizado)

### Justificativa

1. **Requisito obrigatório:** Tech Challenge exige explicitamente Lambda para autenticação
2. **Flexibilidade total:** Validação de CPF customizada (algoritmo específico do Brasil)
3. **Integração com RDS:** Consulta direta na tabela Customers/Admins
4. **Custo:** $0.20 para 1M requests vs $25+ para Auth0
5. **Padrão JWT:** Token portável, compatível com qualquer client (mobile, web, Postman)
6. **API Gateway Authorizer:** Integração nativa para proteger rotas

### Arquitetura da Solução

**Componentes:**

1. **Lambda Login** (.NET 8)
   - Handler: `POST /auth/login`
   - Input: `{"cpf": "12345678901"}`
   - Validações:
     - Formato CPF (11 dígitos, dígitos verificadores)
     - Existência no banco RDS
     - Status ativo/inativo
   - Output: `{"token": "eyJhbG...", "expiresAt": "2026-08-11T12:00:00Z"}`

2. **Lambda Authorizer** (.NET 8)
   - Type: TOKEN authorizer
   - Input: `Authorization: Bearer eyJhbG...`
   - Validações:
     - Assinatura HMAC-SHA256
     - Expiração (exp claim)
     - Issuer/Audience
   - Output: IAM Policy (Allow/Deny)

3. **AWS Secrets Manager**
   - Secret: `autorepair/jwt-secret`
   - Chave: 256 bits (mínimo para HS256)
   - Rotação: Manual (automática em produção futura)

4. **API Gateway**
   - Rota pública: `POST /auth/login`
   - Rotas protegidas: `GET /api/*` (com Authorizer)

### Implementação

**JWT Claims:**
```json
{
  "sub": "uuid-do-usuario",
  "email": "cliente@example.com",
  "role": "Customer",
  "customerId": "uuid",
  "iat": 1693478400,
  "exp": 1693564800,
  "iss": "AutoRepairShop",
  "aud": "AutoRepairShopUsers"
}
```

**Algoritmo:** HMAC-SHA256 (HS256)  
**Expiração:** 24 horas (86400 segundos)  
**Refresh:** Manual por enquanto (futuro: refresh tokens)

### Consequências

**Positivas:**
- Atende 100% dos requisitos do Tech Challenge
- Controle total sobre regras de negócio
- Custo operacional mínimo
- Escalabilidade automática (Lambda)

**Negativas:**
- Responsabilidade de implementar segurança corretamente
- Precisa implementar refresh tokens manualmente (futuro)
- Chave JWT precisa ser gerenciada (Secrets Manager)

### Plano de Segurança

1. **Chave JWT:**
   - Armazenada no AWS Secrets Manager
   - Rotação manual trimestral
   - Acesso via IAM role (Lambda execution role)

2. **Validações:**
   - CPF: Algoritmo de dígitos verificadores
   - Token: Assinatura, expiração, issuer, audience
   - Rate limiting: 100 requests/minuto por IP (API Gateway)

3. **Monitoramento:**
   - CloudWatch Logs: Todas as tentativas de login
   - Alertas: Taxa de falha > 10%
   - Métricas: Latência p95 < 200ms

### Métricas de Sucesso

- Latência login < 200ms (p95)
- Latência authorizer < 100ms (p95)
- Taxa de sucesso > 95%
- Zero vazamento de chaves JWT
- Uptime > 99.9%

### Roadmap Futuro

**Fase 2 (após MVP):**
- Refresh tokens (rotação automática)
- MFA opcional para admins
- OAuth 2.0 (authorization code flow)

**Fase 3 (produção):**
- Rotação automática de chave JWT (Secrets Manager)
- Rate limiting por usuário
- Detecção de anomalias (login de IP suspeito)

---

## RFC-004: Estratégia de Deploy - Kubernetes (EKS)

**Status:** APROVADO  
**Data:** 2026-08-12  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos escolher a plataforma de deploy para a aplicação .NET principal. O Tech Challenge exige **Cluster Kubernetes com escalabilidade**.

### Requisitos Obrigatórios (Tech Challenge)

1. Cluster Kubernetes
2. Escalabilidade (HPA - Horizontal Pod Autoscaler)
3. Alta disponibilidade

### Opções Consideradas

#### 1. AWS EKS (Elastic Kubernetes Service)
**Prós:**
- Kubernetes gerenciado pela AWS
- Control Plane gerenciado (patches, upgrades, HA)
- Integração nativa com ALB, NLB, EBS, IAM
- Multi-AZ nativo
- Versão Kubernetes sempre atualizada
- Suporte a Fargate (serverless nodes)

**Contras:**
- Custo do control plane: $73/mês
- Custo dos worker nodes (EC2): $15-30/mês por node
- Complexidade de configuração inicial
- Curva de aprendizado Kubernetes

#### 2. AWS ECS (Elastic Container Service)
**Prós:**
- Mais simples que Kubernetes
- Integração nativa AWS
- Custo menor (sem control plane fee)
- Fargate disponível

**Contras:**
- **NÃO ATENDE REQUISITO:** Tech Challenge exige Kubernetes
- Menos portável (vendor lock-in AWS)
- Menos features de orquestração
- Comunidade menor

#### 3. Self-managed Kubernetes (EC2)
**Prós:**
- Controle total do cluster
- Sem custo de control plane
- Possibilidade de customização avançada

**Contras:**
- Responsabilidade total por updates, patches, HA
- Complexidade operacional alta
- Risco de downtime por erro humano
- Não recomendado para equipe pequena

#### 4. Kubernetes on-premises
**Prós:**
- Controle total da infraestrutura
- Possível redução de custos a longo prazo

**Contras:**
- **NÃO ATENDE CONTEXTO:** Tech Challenge exige cloud pública
- Custo de hardware, data center, energia
- Complexidade operacional extrema
- Sem elasticidade

### Decisão

**Escolhido:** AWS EKS (Elastic Kubernetes Service)

### Justificativa

1. **Requisito obrigatório:** Tech Challenge exige Kubernetes
2. **Gerenciamento:** AWS cuida do control plane (HA, upgrades, security patches)
3. **Integração:** Nativa com NLB, IAM, CloudWatch, Secrets Manager
4. **Escalabilidade:** HPA + Cluster Autoscaler prontos para uso
5. **Multi-AZ:** Alta disponibilidade nativa
6. **Comunidade:** Kubernetes é padrão da indústria, facilita contratação
---

## Histórico de RFCs

| RFC | Título | Status | Data |
|-----|--------|--------|------|
| RFC-001 | Escolha da Nuvem AWS | APROVADO | 2026-08-01 |
| RFC-002 | Escolha do Banco de Dados - SQL Server | APROVADO | 2026-08-05 |
| RFC-003 | Estratégia de Autenticação - Lambda + JWT | APROVADO | 2026-08-10 |
| RFC-004 | Estratégia de Deploy - Kubernetes (EKS) | APROVADO | 2026-08-12 |

---

**Última atualização:** Agosto 2026  
**Autores:** Dhiulia da Silva, Mateus Pinheiro
