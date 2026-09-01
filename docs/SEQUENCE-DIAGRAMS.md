# 🔄 Diagramas de Sequência - AutoRepairShop

## 1. Fluxo de Autenticação (Login)

```mermaid
sequenceDiagram
    actor Cliente
    participant APIGW as API Gateway
    participant LambdaLogin as Lambda Login
    participant SecretsManager as Secrets Manager
    participant RDS as RDS SQL Server
    
    Cliente->>+APIGW: POST /auth/login<br/>{cpf: "12345678901"}
    APIGW->>+LambdaLogin: Invoke Lambda
    
    Note over LambdaLogin: 1. Validar formato CPF
    
    LambdaLogin->>+SecretsManager: GetSecretValue<br/>(JWT Key)
    SecretsManager-->>-LambdaLogin: JWT Secret Key
    
    LambdaLogin->>+RDS: SELECT * FROM Customers<br/>WHERE Document = '12345678901'
    
    alt Cliente encontrado e ativo
        RDS-->>-LambdaLogin: Customer Data<br/>{id, name, email, status}
        
        Note over LambdaLogin: 2. Gerar JWT Token<br/>Claims: userId, email, role
        
        LambdaLogin-->>-APIGW: 200 OK<br/>{success: true, token: "eyJ..."}
        APIGW-->>-Cliente: JWT Token
        
    else Cliente não encontrado
        RDS-->>LambdaLogin: NULL
        LambdaLogin-->>APIGW: 404 Not Found<br/>{success: false, message: "CPF não encontrado"}
        APIGW-->>Cliente: Erro
        
    else CPF inválido
        LambdaLogin-->>APIGW: 400 Bad Request<br/>{success: false, message: "CPF inválido"}
        APIGW-->>Cliente: Erro
    end
```

---

## 2. Fluxo de Requisição Protegida (com JWT)

```mermaid
sequenceDiagram
    actor Cliente
    participant APIGW as API Gateway
    participant LambdaAuth as Lambda Authorizer
    participant SecretsManager as Secrets Manager
    participant NLB as Network LB
    participant Pod as API Pod (K8s)
    participant RDS as RDS SQL Server
    
    Cliente->>+APIGW: GET /api/customers<br/>Authorization: Bearer eyJ...
    
    Note over APIGW: Extrair token do header
    
    APIGW->>+LambdaAuth: Authorize Request<br/>{authorizationToken: "eyJ..."}
    
    LambdaAuth->>+SecretsManager: GetSecretValue<br/>(JWT Key)
    SecretsManager-->>-LambdaAuth: JWT Secret Key
    
    Note over LambdaAuth: Validar JWT:<br/>- Assinatura<br/>- Expiração<br/>- Claims
    
    alt Token válido
        LambdaAuth-->>-APIGW: IAM Policy: Allow<br/>Context: {userId, email, role}
        
        APIGW->>+NLB: Forward request<br/>+ Context headers
        NLB->>+Pod: HTTP GET /api/customers<br/>X-User-Id: abc123
        
        Pod->>+RDS: SELECT * FROM Customers<br/>WHERE Id = 'abc123'
        RDS-->>-Pod: Customer List
        
        Pod-->>-NLB: 200 OK<br/>[{id, name, email}]
        NLB-->>-APIGW: Response
        APIGW-->>-Cliente: Customer Data
        
    else Token inválido/expirado
        LambdaAuth-->>APIGW: IAM Policy: Deny
        APIGW-->>Cliente: 401 Unauthorized
    end
```

---

## 3. Fluxo de Criação de Ordem de Serviço

```mermaid
sequenceDiagram
    actor Cliente
    participant APIGW as API Gateway
    participant LambdaAuth as Lambda Authorizer
    participant NLB as Network LB
    participant Pod as API Pod
    participant RDS as RDS SQL Server
    
    Cliente->>+APIGW: POST /api/serviceorders<br/>Authorization: Bearer eyJ...<br/>{customerId, vehicleId, services[]}
    
    APIGW->>+LambdaAuth: Authorize
    LambdaAuth-->>-APIGW: Allow (userId: abc123)
    
    APIGW->>+NLB: Forward request
    NLB->>+Pod: POST /api/serviceorders
    
    Note over Pod: 1. Validar dados de entrada
    
    Pod->>+RDS: BEGIN TRANSACTION
    
    Pod->>RDS: SELECT * FROM Customers<br/>WHERE Id = 'customerId'
    RDS-->>Pod: Customer exists
    
    Pod->>RDS: SELECT * FROM Vehicles<br/>WHERE Id = 'vehicleId'<br/>AND CustomerId = 'customerId'
    RDS-->>Pod: Vehicle exists
    
    Pod->>RDS: INSERT INTO ServiceOrders<br/>(CustomerId, VehicleId, Status, CreatedAt)
    RDS-->>Pod: ServiceOrder created (orderId)
    
    loop Para cada serviço
        Pod->>RDS: INSERT INTO ServiceOrderServices<br/>(ServiceOrderId, ServiceId)
        RDS-->>Pod: Service linked
    end
    
    Pod->>RDS: COMMIT TRANSACTION
    RDS-->>-Pod: Transaction committed
    
    Note over Pod: 2. Gerar response com ID da ordem
    
    Pod-->>-NLB: 201 Created<br/>{orderId, status, createdAt}
    NLB-->>-APIGW: Response
    APIGW-->>-Cliente: Ordem criada com sucesso
```

---

## 4. Fluxo de Auto-Scaling (HPA)

```mermaid
sequenceDiagram
    participant Requests as Múltiplas Requisições
    participant NLB as Network LB
    participant Pod1 as API Pod 1
    participant Pod2 as API Pod 2
    participant MetricsServer as Metrics Server
    participant HPA as HPA Controller
    participant K8s as Kubernetes API
    
    loop Tráfego alto
        Requests->>NLB: 100 req/s
        NLB->>Pod1: Load balance
        NLB->>Pod2: Load balance
        
        Note over Pod1,Pod2: CPU > 50%
    end
    
    MetricsServer->>MetricsServer: Coletar métricas de CPU/Memory
    
    HPA->>+MetricsServer: GetMetrics(deployment/api)
    MetricsServer-->>-HPA: CPU: 75%, Memory: 60%
    
    Note over HPA: Calcular réplicas necessárias<br/>Desired = ceil(current * (75/50)) = 2
    
    HPA->>+K8s: PATCH Deployment/api<br/>replicas: 2 → 3
    K8s-->>-HPA: Deployment updated
    
    K8s->>K8s: Criar novo Pod
    
    Note over K8s: Pod3 criado e em Running
    
    NLB->>NLB: Adicionar Pod3 ao target group
    
    loop Tráfego normalizado
        Requests->>NLB: 30 req/s
        NLB->>Pod1: Load balance
        NLB->>Pod2: Load balance
        NLB->>Pod1: Load balance (Pod3)
        
        Note over Pod1,Pod2: CPU < 30%
    end
    
    HPA->>K8s: PATCH Deployment/api<br/>replicas: 3 → 1
    
    Note over K8s: Pods 2 e 3 removidos gradualmente
```

---

## 5. Fluxo de Deploy via CI/CD

```mermaid
sequenceDiagram
    actor Dev as Desenvolvedor
    participant Git as GitHub
    participant GHA as GitHub Actions
    participant Docker as Docker Registry (GHCR)
    participant Terraform as Terraform
    participant AWS as AWS
    participant K8s as Kubernetes (EKS)
    
    Dev->>+Git: git push origin master<br/>(código da API)
    Git->>+GHA: Trigger workflow CD
    
    Note over GHA: Job 1: Build
    
    GHA->>GHA: Setup .NET 8
    GHA->>GHA: dotnet restore
    GHA->>GHA: dotnet build
    GHA->>GHA: dotnet publish -c Release
    
    GHA->>GHA: docker build -t api:latest
    GHA->>+Docker: docker push ghcr.io/autorepairorg/api:latest
    Docker-->>-GHA: Image pushed
    
    Note over GHA: Job 2: Deploy
    
    GHA->>+AWS: Configure AWS Credentials
    AWS-->>-GHA: Authenticated
    
    GHA->>+K8s: kubectl set image<br/>deployment/api api=ghcr.io/.../api:latest
    
    K8s->>K8s: Rolling update<br/>- Criar novo Pod<br/>- Aguardar health check<br/>- Remover Pod antigo
    
    K8s-->>-GHA: Deployment successful
    
    GHA->>K8s: kubectl get svc api-nlb
    K8s-->>GHA: LoadBalancer URL
    
    GHA-->>-Git: ✅ Deploy completed
    Git-->>-Dev: Notificação de sucesso
```

---

## 6. Fluxo de Recuperação de Falha (Health Check)

```mermaid
sequenceDiagram
    participant NLB as Network LB
    participant Pod as API Pod
    participant K8s as Kubernetes
    participant RDS as RDS SQL Server
    
    loop Health Check (a cada 10s)
        NLB->>+Pod: GET /health
        
        alt Pod saudável
            Pod->>+RDS: SELECT 1 (test connection)
            RDS-->>-Pod: OK
            Pod-->>-NLB: 200 OK<br/>{status: "healthy"}
            NLB->>NLB: Manter Pod no pool
            
        else Pod não responde
            Note over Pod: Crash / Deadlock
            Pod--xNLB: Timeout (30s)
            NLB->>NLB: Remover Pod do pool
            
            Note over NLB: Rotear tráfego<br/>para outros Pods
            
            K8s->>K8s: Detectar Pod não healthy
            K8s->>K8s: Restart Pod
            
            Note over Pod: Pod reiniciado
            
            Pod->>K8s: Ready
            K8s->>NLB: Adicionar Pod ao pool
        end
    end
```

---

## 7. Fluxo de Backup e Restore (RDS)

```mermaid
sequenceDiagram
    participant AWS as AWS RDS
    participant RDS as RDS Instance
    participant S3 as S3 (Backups)
    participant Admin as Administrador
    
    Note over AWS: Backup Automático Diário<br/>(03:00 UTC)
    
    AWS->>+RDS: Criar snapshot
    RDS->>RDS: Freeze writes momentaneamente
    RDS->>+S3: Upload snapshot
    S3-->>-RDS: Snapshot stored
    RDS-->>-AWS: Backup completo
    
    Note over S3: Retenção: 7 dias
    
    alt Disaster Recovery Necessário
        Admin->>+AWS: RestoreDBInstanceFromSnapshot
        AWS->>+S3: Baixar snapshot
        S3-->>-AWS: Snapshot data
        
        AWS->>AWS: Criar nova RDS instance
        
        Note over AWS: ~15-30 minutos
        
        AWS-->>-Admin: Nova instância disponível<br/>Endpoint: new-rds.amazonaws.com
        
        Admin->>Admin: Atualizar ConfigMap K8s<br/>com novo endpoint
        Admin->>K8s: kubectl apply -f configmap.yaml
        K8s->>Pods: Rolling restart
        
        Note over Pods: Conectam no novo RDS
    end
```

---

## 8. Fluxo de Monitoramento e Alertas

```mermaid
sequenceDiagram
    participant Pod as API Pod
    participant CloudWatch as CloudWatch
    participant Datadog as Datadog Agent
    participant Dashboard as Datadog Dashboard
    participant SNS as AWS SNS
    participant Email as E-mail/Slack
    
    loop A cada request
        Pod->>Pod: Processar request
        Pod->>+CloudWatch: Log estruturado<br/>{timestamp, level, message}
        Pod->>+Datadog: Metrics<br/>(latência, CPU, memória)
    end
    
    CloudWatch->>CloudWatch: Agregar métricas
    Datadog->>+Dashboard: Atualizar dashboard<br/>em tempo real
    
    alt Anomalia detectada
        Note over CloudWatch: Error rate > 5%
        CloudWatch->>+SNS: Trigger alarm
        SNS->>+Email: Enviar alerta<br/>"High error rate detected"
        
        Note over Dashboard: Latência p95 > 500ms
        Dashboard->>Dashboard: Marcar anomalia
        Dashboard->>Email: Enviar notificação Slack
    end
    
    Dashboard-->>-Pod: Visualização de métricas
    CloudWatch-->>-Pod: Logs centralizados
```

---

## Resumo dos Fluxos

| Fluxo | Atores Principais | Tempo Típico |
|-------|-------------------|--------------|
| Autenticação | Cliente, Lambda, RDS | ~200ms |
| Requisição Protegida | Cliente, Authorizer, API Pod | ~150ms |
| Criar Ordem de Serviço | Cliente, API Pod, RDS | ~300ms |
| Auto-Scaling | HPA, Metrics Server, K8s | ~30-60s |
| Deploy CI/CD | GitHub Actions, K8s | ~5-8min |
| Health Check | NLB, Pod, RDS | ~10s (intervalo) |
| Backup | RDS, S3 | ~15min (diário) |
| Restore | Admin, RDS, K8s | ~30min |
| Monitoramento | CloudWatch, Datadog | Tempo real |

---

## Tratamento de Erros

### **Retry Strategy**
- Lambda: 2 retries automáticos (exponential backoff)
- API Pod → RDS: 3 retries com 1s de intervalo
- NLB Health Check: 3 falhas consecutivas → remove do pool

### **Circuit Breaker**
- API → RDS: Circuito abre após 5 falhas consecutivas
- Timeout: 30s
- Half-open após 60s

### **Fallback**
- Lambda Login: Retorna 503 se RDS indisponível
- API Pod: Cache de leitura para dados estáticos (futuro)

---

**Última atualização:** Agosto 2026  
**Autores:** Dhiulia da Silva, Mateus Pinheiro
