# ADRs - Architecture Decision Records

Registro de decisões arquiteturais permanentes do projeto AutoRepairShop.

---

## ADR-001: Padrão de Comunicação REST API

**Status:** ACEITO  
**Data:** 2026-08-01  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos definir o padrão de comunicação entre clientes (mobile/web) e o backend.

### Decisão

Adotar **REST API** com JSON como formato de dados.

### Justificativa

1. **Simplicidade:** HTTP verbs (GET, POST, PUT, DELETE) são intuitivos
2. **Compatibilidade:** Suportado nativamente por todas as plataformas (mobile, web, IoT)
3. **Ferramentas:** Swagger/OpenAPI para documentação automática
4. **Stateless:** Cada request é independente, facilita escalabilidade
5. **Caching:** HTTP cache nativo (ETags, Cache-Control)
6. **Familiaridade:** Equipe já domina REST

### Alternativas Consideradas

**GraphQL:**
- Prós: Query flexível, reduz over-fetching
- Contras: Complexidade adicional, overhead de aprendizado, caching mais complexo

**gRPC:**
- Prós: Performance superior (Protocol Buffers), streaming bidirecional
- Contras: Menos suporte em browsers, debugging difícil, incompatível com API Gateway REST

**SOAP:**
- Prós: Contratos rígidos (WSDL), segurança WS-Security
- Contras: Verboso (XML), lento, ultrapassado

### Consequências

**Positivas:**
- Documentação automática via Swagger
- Fácil integração com API Gateway
- Postman/Insomnia para testes
- Caching HTTP padrão

**Negativas:**
- Over-fetching em algumas queries (mitigado com DTOs específicos)
- Múltiplas chamadas para dados relacionados (mitigado com includes)

### Implementação

**Convenções adotadas:**

1. **Versionamento:** `/api/v1/customers` (futuro)
2. **Recursos:** Substantivos no plural (`/customers`, `/serviceorders`)
3. **HTTP Status:**
   - 200: Sucesso (GET, PUT)
   - 201: Criado (POST)
   - 204: Sem conteúdo (DELETE)
   - 400: Validação falhou
   - 401: Não autenticado
   - 403: Não autorizado
   - 404: Não encontrado
   - 500: Erro interno

4. **Paginação:** Query params (`?page=1&size=20`)
5. **Filtros:** Query params (`?status=pending&minDate=2026-08-01`)
6. **Ordenação:** Query param (`?orderBy=createdAt&direction=desc`)

**Exemplo de endpoints:**
```
GET    /api/customers              - Listar clientes
GET    /api/customers/{id}         - Obter cliente
POST   /api/customers              - Criar cliente
PUT    /api/customers/{id}         - Atualizar cliente
DELETE /api/customers/{id}         - Deletar cliente

GET    /api/serviceorders          - Listar ordens
POST   /api/serviceorders          - Criar ordem
PATCH  /api/serviceorders/{id}     - Atualizar status
```

### Métricas de Sucesso

- Latência p95 < 200ms
- Taxa de erro 4xx < 5%
- Taxa de erro 5xx < 1%
- Swagger atualizado automaticamente

---

## ADR-002: Uso de HPA (Horizontal Pod Autoscaler)

**Status:** ACEITO  
**Data:** 2026-08-03  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos garantir escalabilidade automática da aplicação conforme demanda varia.

### Decisão

Implementar **HPA (Horizontal Pod Autoscaler)** baseado em CPU utilization.

### Justificativa

1. **Requisito obrigatório:** Tech Challenge exige escalabilidade no Kubernetes
2. **Custo-eficiência:** Escala down em períodos ociosos (economia de recursos)
3. **Performance:** Escala up automaticamente sob carga (sem intervenção manual)
4. **Nativo Kubernetes:** Sem necessidade de ferramentas externas
5. **Simples:** Configuração declarativa via YAML

### Configuração

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: api-hpa
  namespace: oficina
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: api
  minReplicas: 1
  maxReplicas: 5
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 50
```

**Parâmetros escolhidos:**
- **Min replicas:** 1 (custo mínimo em períodos ociosos)
- **Max replicas:** 5 (limite para evitar custo descontrolado)
- **Target CPU:** 50% (equilíbrio entre performance e custo)
- **Scale up:** Agressivo (adiciona 1 pod quando CPU > 50%)
- **Scale down:** Conservador (aguarda 5min de baixa utilização)

### Alternativas Consideradas

**VPA (Vertical Pod Autoscaler):**
- Prós: Ajusta requests/limits automaticamente
- Contras: Requer restart de pod, menos previsível
- Decisão: Pode ser usado futuramente em conjunto com HPA

**KEDA (Kubernetes Event Driven Autoscaler):**
- Prós: Escala baseado em eventos (SQS, Kafka, HTTP requests)
- Contras: Complexidade adicional, overkill para MVP
- Decisão: Avaliar em Fase 2 se houver workloads assíncronos

**Cluster Autoscaler:**
- Prós: Escala nodes EC2 conforme necessidade de pods
- Contras: Custo de EKS node adicional, lentidão (demora ~5min)
- Decisão: Implementar em Fase 2 quando cluster tiver múltiplos workloads

### Consequências

**Positivas:**
- Escalabilidade automática sem intervenção manual
- Custo otimizado (1 pod idle vs 5 pods sob carga)
- SLA mantido mesmo em picos de tráfego
- Configuração simples (15 linhas de YAML)

**Negativas:**
- Depende de Metrics Server (precisa instalar separadamente)
- Scale down pode causar latência temporária se tráfego subir rapidamente
- Apenas CPU como métrica (memória/custom metrics precisam de config adicional)

### Plano de Monitoramento

**Métricas CloudWatch:**
- `kube_hpa_status_current_replicas`
- `kube_hpa_status_desired_replicas`
- `kube_hpa_status_condition`

**Alertas:**
- HPA atingiu max replicas (avaliar aumentar limite)
- HPA não conseguiu escalar (verificar Metrics Server)
- Scale up/down muito frequente (ajustar threshold)

### Roadmap Futuro

**Fase 2:**
- Adicionar métrica de memória ao HPA
- Custom metrics via Datadog (requests/segundo, latência)

**Fase 3:**
- KEDA para workers assíncronos
- Cluster Autoscaler para múltiplos workloads

### Métricas de Sucesso

- HPA escala up em < 60 segundos quando CPU > 50%
- HPA escala down após 5 minutos de CPU < 30%
- Zero requests rejeitados por falta de capacity
- Custo médio de pods < $30/mês

---

## ADR-003: Armazenamento de Secrets com AWS Secrets Manager

**Status:** ACEITO  
**Data:** 2026-08-05  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos armazenar secrets sensíveis (JWT key, database password, API keys) de forma segura.

### Decisão

Usar **AWS Secrets Manager** para armazenamento de secrets.

### Justificativa

1. **Segurança:** Criptografia em repouso (KMS), auditoria (CloudTrail)
2. **Rotação automática:** Suporte nativo para RDS, Lambda, outros
3. **Integração:** IAM policies granulares, acesso via SDK
4. **Versionamento:** Histórico de valores anteriores
5. **Compliance:** Atende requisitos de segurança corporativa

### Secrets Armazenados

**1. JWT Secret Key**
- Path: `autorepair/jwt-secret`
- Formato: `{"key": "base64-encoded-256-bit-key"}`
- Acesso: Lambda Login + Lambda Authorizer
- Rotação: Manual trimestral (automática em produção)

**2. RDS Credentials**
- Path: `autorepair/rds-credentials`
- Formato: `{"username": "admin", "password": "...", "host": "...", "port": 1433}`
- Acesso: API Pods (via IRSA)
- Rotação: Automática (RDS managed)

**3. Datadog API Key** (futuro)
- Path: `autorepair/datadog-key`
- Formato: `{"apiKey": "...", "appKey": "..."}`
- Acesso: Datadog agent (K8s)
- Rotação: Manual

### Alternativas Consideradas

**Kubernetes Secrets:**
- Prós: Nativo Kubernetes, fácil de usar
- Contras: Base64 encoding (não é criptografia), sem rotação automática, auditoria limitada
- Decisão: Usar apenas para secrets não-sensíveis (ConfigMaps)

**AWS Systems Manager Parameter Store:**
- Prós: Mais barato ($0 para standard parameters), integração similar
- Contras: Sem rotação automática nativa, limite de 10.000 parâmetros, menos features
- Decisão: Usar para configurações não-sensíveis (futuro)

**HashiCorp Vault:**
- Prós: Multi-cloud, dynamic secrets, políticas avançadas
- Contras: Complexidade operacional alta, custo de infra, overkill para MVP
- Decisão: Avaliar em Fase 3 se houver necessidade multi-cloud

**External Secrets Operator:**
- Prós: Sincroniza Secrets Manager → Kubernetes Secrets automaticamente
- Contras: Componente adicional no cluster, ponto de falha extra
- Decisão: Implementar em Fase 2 para simplificar acesso dos pods

### Implementação

**Acesso via IAM Roles:**

**Lambda (execution role):**
```json
{
  "Effect": "Allow",
  "Action": [
    "secretsmanager:GetSecretValue",
    "secretsmanager:DescribeSecret"
  ],
  "Resource": "arn:aws:secretsmanager:us-east-1:*:secret:autorepair/*"
}
```

**EKS Pods (via IRSA):**
```json
{
  "Effect": "Allow",
  "Action": [
    "secretsmanager:GetSecretValue"
  ],
  "Resource": "arn:aws:secretsmanager:us-east-1:*:secret:autorepair/rds-credentials"
}
```

**Código exemplo (.NET Lambda):**
```csharp
var client = new AmazonSecretsManagerClient(RegionEndpoint.USEast1);
var request = new GetSecretValueRequest { SecretId = "autorepair/jwt-secret" };
var response = await client.GetSecretValueAsync(request);
var secret = JsonSerializer.Deserialize<JwtSecret>(response.SecretString);
```

### Consequências

**Positivas:**
- Secrets nunca em código-fonte ou variáveis de ambiente plaintext
- Auditoria completa (quem acessou qual secret, quando)
- Rotação automática de RDS passwords
- Compliance com boas práticas de segurança

**Negativas:**
- Custo: $0.40/secret/mês + $0.05 por 10.000 chamadas
- Latência adicional (~50ms) ao buscar secret
- Dependência da AWS (vendor lock-in)

### Plano de Segurança

**Rotação:**
- JWT Key: Manual trimestral (ou após suspeita de vazamento)
- RDS Password: Automática via Secrets Manager (mensal)
- Datadog Keys: Manual semestral

**Auditoria:**
- CloudTrail logs de todos os acessos
- Alerta se acesso fora de horário comercial
- Review trimestral de IAM policies

**Contingência:**
- Backup manual de secrets em cofre criptografado (offline)
- Runbook para rotação emergencial

### Métricas de Sucesso

- Zero secrets em código-fonte (validado por SAST)
- 100% de secrets sensíveis no Secrets Manager
- Latência de GetSecretValue < 100ms
- Zero vazamentos de secrets (monitorado por git-secrets)

---

## ADR-004: Estratégia de Logs com CloudWatch

**Status:** ACEITO  
**Data:** 2026-08-07  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos coletar, centralizar e analisar logs de todos os componentes (Lambda, EKS, RDS) para troubleshooting e monitoramento.

### Decisão

Usar **AWS CloudWatch Logs** como solução de logging centralizado.

### Justificativa

1. **Integração nativa:** Lambda, EKS, RDS já enviam logs automaticamente
2. **Custo:** $0.50/GB ingestão + $0.03/GB armazenamento (mais barato que Datadog Logs)
3. **Retention:** Configurável por log group (1 dia a 10 anos)
4. **Queries:** CloudWatch Logs Insights (SQL-like)
5. **Alertas:** CloudWatch Alarms baseados em métricas de logs

### Estrutura de Log Groups

**Lambda Functions:**
- `/aws/lambda/autorepair-login`
- `/aws/lambda/autorepair-authorizer`
- Retention: 7 dias
- Filtros: ERROR, WARN

**EKS Pods:**
- `/aws/eks/autorepairshop-eks/cluster`
- `/aws/containerinsights/autorepairshop-eks/application`
- Retention: 14 dias
- Formato: JSON estruturado

**RDS:**
- `/aws/rds/instance/autorepair-sqlserver/error`
- `/aws/rds/instance/autorepair-sqlserver/slowquery`
- Retention: 30 dias

### Formato de Logs Estruturados

**JSON Schema:**
```json
{
  "timestamp": "2026-08-07T10:30:00.123Z",
  "level": "INFO|WARN|ERROR",
  "message": "Customer authenticated successfully",
  "context": {
    "customerId": "uuid",
    "requestId": "abc123",
    "ipAddress": "203.0.113.42",
    "userAgent": "Mozilla/5.0...",
    "latencyMs": 145
  },
  "exception": {
    "type": "SqlException",
    "message": "Connection timeout",
    "stackTrace": "..."
  }
}
```

**Níveis de log:**
- **TRACE:** Detalhes internos (disabled em produção)
- **DEBUG:** Informações de desenvolvimento
- **INFO:** Eventos normais (login, criação de ordem)
- **WARN:** Situações incomuns mas recuperáveis (retry bem-sucedido)
- **ERROR:** Erros que requerem atenção (falha de autenticação, timeout DB)
- **FATAL:** Erros críticos (aplicação crashou)

### Alternativas Consideradas

**ELK Stack (Elasticsearch, Logstash, Kibana):**
- Prós: Queries poderosas, visualizações ricas, open source
- Contras: Custo de infra ($100+/mês), complexidade operacional, precisa gerenciar cluster
- Decisão: Overkill para MVP, avaliar em Fase 3

**Datadog Logs:**
- Prós: APM integrado, dashboards bonitos, alertas avançados
- Contras: Custo elevado ($0.10/GB ingestão + $1.27/milhão eventos), vendor lock-in
- Decisão: Usar Datadog apenas para métricas/APM, não logs

**Splunk:**
- Prós: Líder de mercado, features avançadas (SIEM, correlação)
- Contras: Custo proibitivo ($150+/GB), complexidade, enterprise-only
- Decisão: Não adequado para startup/MVP

**Loki (Grafana):**
- Prós: Leve, compatível com Prometheus, custo baixo
- Contras: Menos features que CloudWatch Insights, precisa hospedar
- Decisão: Avaliar em Fase 2 se Grafana for adotado

### Implementação

**Lambda (.NET):**
```csharp
ILogger<Function> logger;

logger.LogInformation("Customer {CustomerId} authenticated", customerId);
logger.LogError(ex, "Failed to generate JWT for customer {CustomerId}", customerId);
```

**EKS Pod (.NET):**
```csharp
// Serilog + CloudWatch sink
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.AWSSerilog(configuration)
    .CreateLogger();

Log.Information("ServiceOrder {OrderId} created for customer {CustomerId}", orderId, customerId);
```

**CloudWatch Logs Insights Queries:**

**Top 10 erros:**
```
fields @timestamp, message, context.customerId
| filter level = "ERROR"
| stats count() by exception.type
| sort count desc
| limit 10
```

**Latência p95:**
```
fields @timestamp, context.latencyMs
| filter ispresent(context.latencyMs)
| stats pct(context.latencyMs, 95) as p95
```

### Consequências

**Positivas:**
- Logs centralizados de todos os componentes
- Queries SQL-like para análise
- Integração nativa AWS (zero configuração)
- Custo controlado com retention policies

**Negativas:**
- CloudWatch Insights menos poderoso que Elasticsearch
- Custo cresce com volume ($0.50/GB)
- Sem visualizações prontas (precisa criar queries manualmente)

### Plano de Custos

**Estimativa mensal:**
- Lambda logs: 1 GB/mês = $0.50
- EKS logs: 5 GB/mês = $2.50
- RDS logs: 2 GB/mês = $1.00
- Queries: 100 queries/mês = $0.50
- **Total:** ~$5/mês

**Otimizações:**
- Filtrar logs DEBUG em produção
- Retention curta para logs INFO (7 dias)
- Retention longa para logs ERROR (30 dias)
- Exportar logs antigos para S3 ($0.023/GB)

### Alertas Configurados

**Critical:**
- Taxa de erro > 5% (SNS → E-mail)
- Lambda timeout > 3x em 5 minutos
- RDS connection pool esgotado

**Warning:**
- Latência p95 > 500ms
- Taxa de retry > 10%
- Disco RDS > 70%

### Métricas de Sucesso

- 100% dos componentes enviando logs para CloudWatch
- Tempo médio de troubleshooting < 15 minutos
- Zero perda de logs
- Custo de logs < $10/mês

---

## ADR-005: Uso de Network Load Balancer (NLB)

**Status:** ACEITO  
**Data:** 2026-08-09  
**Autor:** Dhiulia da Silva, Mateus Pinheiro

### Contexto

Precisamos expor a aplicação Kubernetes para internet através de um Load Balancer.

### Decisão

Usar **Network Load Balancer (NLB)** ao invés de Application Load Balancer (ALB).

### Justificativa

1. **Latência ultra-baixa:** Layer 4 (TCP) é mais rápido que Layer 7 (HTTP)
2. **IP estático:** NLB mantém IP fixo (facilitawhitelist de IPs)
3. **Throughput:** Suporta milhões de requests/segundo
4. **Simplicidade:** Menos overhead de processamento
5. **Custo:** Ligeiramente mais barato ($16/mês vs $18/mês para ALB)

### Alternativas Consideradas

**Application Load Balancer (ALB):**
- Prós: Path-based routing, host-based routing, WAF integration, HTTP/2, WebSockets
- Contras: Latência maior (~10ms overhead), custo maior, complexidade desnecessária
- Decisão: Não precisamos de routing avançado (API Gateway faz isso)

**Classic Load Balancer:**
- Prós: Simples, barato
- Contras: Legacy (deprecated pela AWS), menos features
- Decisão: AWS recomenda NLB/ALB para novos projetos

**Ingress Controller (nginx/traefik):**
- Prós: Kubernetes-native, features avançadas (canary, A/B testing)
- Contras: Complexidade adicional, precisa gerenciar pods do controller, NLB por trás anyway
- Decisão: Overkill para MVP, avaliar em Fase 2

### Configuração

**Service Kubernetes:**
```yaml
apiVersion: v1
kind: Service
metadata:
  name: api-nlb
  namespace: oficina
  annotations:
    service.beta.kubernetes.io/aws-load-balancer-type: "nlb"
    service.beta.kubernetes.io/aws-load-balancer-scheme: "internet-facing"
    service.beta.kubernetes.io/aws-load-balancer-cross-zone-load-balancing-enabled: "true"
spec:
  type: LoadBalancer
  selector:
    app: api
  ports:
  - name: http
    port: 80
    targetPort: 8080
    protocol: TCP
  externalTrafficPolicy: Cluster
```

**Parâmetros:**
- **Type:** Network (Layer 4)
- **Scheme:** Internet-facing (público)
- **Cross-AZ:** Enabled (distribui tráfego entre AZs)
- **Health check:** TCP port 8080 (cada 10s)
- **Deregistration delay:** 30s

### Consequências

**Positivas:**
- Latência mínima (< 5ms overhead)
- IP estático para DNS
- Suporta picos de tráfego sem degradação
- Custo fixo ($16/mês) independente de requests

**Negativas:**
- Sem WAF nativo (precisa usar API Gateway para isso)
- Sem path-based routing (API Gateway faz)
- Sem SSL termination no NLB (terminado no pod ou API Gateway)

### Plano de Segurança

**Security Group (NLB):**
- Ingress: 0.0.0.0/0 porta 80 (HTTP público)
- Egress: VPC CIDR porta 8080 (para pods)

**Futuro (HTTPS):**
- Certificado ACM
- Listener 443 → 8080
- Redirect 80 → 443

### Métricas de Sucesso

- Latência NLB → Pod < 10ms
- Health check success rate > 99%
- Zero downtime em deploys (rolling update)
- Uptime > 99.9%

---

## Histórico de ADRs

| ADR | Título | Status | Data |
|-----|--------|--------|------|
| ADR-001 | Padrão de Comunicação REST API | ACEITO | 2026-08-01 |
| ADR-002 | Uso de HPA (Horizontal Pod Autoscaler) | ACEITO | 2026-08-03 |
| ADR-003 | Armazenamento de Secrets com AWS Secrets Manager | ACEITO | 2026-08-05 |
| ADR-004 | Estratégia de Logs com CloudWatch | ACEITO | 2026-08-07 |
| ADR-005 | Uso de Network Load Balancer (NLB) | ACEITO | 2026-08-09 |

---

**Última atualização:** Agosto 2026  
**Autores:** Dhiulia da Silva, Mateus Pinheiro
---

Agora vamos criar o **Diagrama ER + Justificativa do Banco**? 🚀
