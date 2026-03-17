# 🟢 RfidRastroVerde  
### Multi-Reader RFID System (WinForms + CLI)

Sistema profissional de leitura RFID com **controle por bandeja**, integração com **painel React**, captura de imagem e comunicação via **API local e cloud**.

Projetado para ambientes **industriais, logísticos e de rastreabilidade**, o sistema garante ciclos de leitura confiáveis, deduplicação global e operação assistida em tempo real.

---

## 🚀 Principais Funcionalidades

- Leitura RFID multi-leitor (HID)
- Deduplicação global de tags
- Sessão por bandeja (ciclo completo de leitura)
- Interface operacional em **React**
- Comunicação em tempo real via **API local (bridge)**
- Captura de imagem da bandeja (workflow integrado)
- Snapshot estruturado por sessão
- Fila de envio assíncrona (resiliente)
- Execução com interface (WinForms) ou modo CLI
- Exportação de dados (XML)
- Logs performáticos e seguros

---

## 🧠 Arquitetura Geral

```text
[ Leitores RFID ]
        ↓
[ WinForms (.NET Framework) ]
  - leitura
  - sessão por bandeja
  - snapshot
  - fila de envio
  - API local (HttpListener)
        ↓
[ API Local - http://localhost:8085 ]
        ↓
[ Front React ]
  - operação
  - status em tempo real
  - configurações
        ↓
[ API Cloud (opcional) ]
```

---

## 🔄 Fluxo Operacional

1. Operador posiciona a bandeja
2. Sistema inicia leitura (automático ou manual)
3. Tags são associadas à sessão atual
4. Operador captura imagem da bandeja
5. Sistema finaliza a sessão
6. Snapshot é gerado
7. Snapshot é enviado ou enfileirado
8. Front acompanha tudo em tempo real

---

## 🌐 API Local (Bridge)

Base:
http://localhost:8085/api

Endpoints:
GET /status
GET /session/current
POST /session/start
POST /session/reset
POST /session/capture-photo
GET /settings
PUT /settings

Essa API permite que o front React controle e monitore o sistema WinForms em tempo real.

---

## 🖥️ Interface WinForms

Responsável por:

- Comunicação com leitores RFID
- Gerenciamento de sessão
- Deduplicação global
- Geração de snapshot
- Envio para API
- Controle de estado

---

## ⚛️ Front React

Responsável por:

- Painel operacional
- Exibição de progresso
- Status da leitura
- Captura de foto
- Configuração do sistema
- Notificações operacionais

---

## 📄 Exportação de Dados (XML)

O sistema permite exportar todas as informações coletadas para um arquivo XML estruturado, contendo:

- Metadados do ciclo de leitura
- Leitores utilizados
- Tags lidas (com métricas completas)
- Log completo da execução (opcional)

### Exemplo simplificado

```xml
<RfidReadReport>
  <Meta>
    <GeneratedAt>2026-01-15T14:32:10.123</GeneratedAt>
    <TotalRows>120</TotalRows>
    <TotalReaders>2</TotalReaders>
  </Meta>
  <Readers>
    <Reader index="0" sn="C38F2410172604">
      <Tags>
        <Tag>
          <Epc>E280F3...</Epc>
          <Antenna>1</Antenna>
          <Rssi>88 (0x58)</Rssi>
          <SeenCount>3</SeenCount>
          <FirstSeen>14:30:01.123</FirstSeen>
          <LastSeen>14:30:05.456</LastSeen>
        </Tag>
      </Tags>
    </Reader>
  </Readers>
</RfidReadReport>
```

---

## 💻 Modo CLI (Linha de Comando)

O projeto também pode ser executado sem interface gráfica, ideal para automação, testes ou integração em pipelines.

Exemplo de uso:

    RfidRastroVerde.exe 100 80

Parâmetros:

    100 → Meta global de tags únicas

    80 → Intervalo de polling em milissegundos

Funcionalidades no CLI:

    Detecção automática de leitores conectados

    Exibição de leituras em tempo real

    Progresso global de tags únicas

    Encerramento automático ao atingir a meta

    Parada manual pressionando Q

---

## ⚙️ Configurações

As configurações são persistidas localmente:
- cliente
- zona
- setor
- meta por bandeja
- API cloud
- flags operacionais

---

## 🧩 Requisitos

- Windows
- .NET Framework 4.7.2
- Node.js (para front)
- Leitores RFID compatíveis
- Visual Studio 2022

---

## ▶️ Como rodar

- WinForms
  - Compilar e executar no Windows:
  - bin/x64/Release/RfidRastroVerde.exe
- Front React
  - npm install
  - npm run dev

---

## 🌐 Integração com API (Opcional)

O sistema suporta envio assíncrono das leituras para uma API REST.
Como funciona:

    Cada tag única gera um DTO (TagReadDto)

    Os dados são enviados por uma fila interna

    O envio não bloqueia leitura nem UI

    A API pode ser habilitada ou desabilitada por configuração

    Exemplo de configuração:

    {
      "BaseUrl": "https://api.exemplo.com",
      "DeviceId": "RFID-01",
      "Enabled": true
    }

    Se BaseUrl estiver vazia ou Enabled = false, a API é automaticamente desativada.

---

## 📌 Observações Importantes

    O sistema sempre reinicia o estado ao iniciar um novo ciclo

    Não há reaproveitamento de leituras anteriores

    Funciona corretamente com 1 ou múltiplos leitores

    Leituras duplicadas entre leitores são tratadas corretamente

    Projetado para ambientes industriais e operação contínua

---

## 📈 Status do Projeto

    🟢 Leitura RFID funcional

    🟢 Sessão por bandeja funcional

    🟢 API local (bridge) funcional

    🟢 Front React integrado

    🟡 Captura de imagem em evolução

    🔧 Em validação operacional

---

## 👤 Autor

Nicolas Zapelão

Projeto: RfidRastroVerde

---

## 📄 Licença

Este projeto pode ser licenciado conforme a necessidade do cliente ou uso interno.
Entre em contato para mais informações.
