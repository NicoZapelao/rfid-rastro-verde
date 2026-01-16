🟢 RfidRastroVerde
Multi-Reader RFID System (WinForms + CLI)

Sistema profissional de leitura RFID multi-leitor, com deduplicação global, visualização em tempo real, exportação estruturada de dados e integração opcional via API REST.

Projetado para ambientes industriais, logísticos e de rastreabilidade, o sistema suporta múltiplos leitores RFID HID conectados simultaneamente, garantindo ciclos de leitura confiáveis, controle total do estado interno e eliminação de leituras fantasmas entre execuções.

🚀 Principais Funcionalidades

✅ Detecção automática de leitores RFID conectados via USB (HID)

✅ Suporte nativo a 1 ou múltiplos leitores simultâneos

✅ Deduplicação GLOBAL de tags (independente do leitor ou antena)

✅ Identificação completa por leitura:

EPC da tag
Antena utilizada
RSSI (força do sinal)
Leitor responsável (Index + Serial Number)

✅ Interface gráfica WinForms com atualização em tempo real

✅ Modo CLI (linha de comando) para automação e uso headless

✅ Exportação estruturada para XML

✅ Integração opcional com API REST (fila assíncrona, não bloqueante)

✅ Sistema de log performático (não trava UI)

✅ Reset completo e seguro entre ciclos de leitura

🧠 Arquitetura Geral

O projeto é organizado em camadas bem definidas, facilitando manutenção, expansão e uso em diferentes cenários:

RfidRastroVerde
├── Driver_Proj
│   ├── RfidReaderDriver.cs      # Comunicação direta com o leitor RFID
│   └── RfidReaderManager.cs     # Gerenciamento multi-leitor + lógica global
│
├── Models_Proj
│   ├── TagRead.cs               # Modelo interno de leitura RFID
│   └── TagRow.cs                # Modelo para visualização no grid
│
├── Api
│   ├── ApiClient.cs             # Cliente HTTP (REST)
│   ├── ApiQueue.cs              # Fila assíncrona de envio
│   ├── ApiConfig.cs             # Configurações da API
│   └── TagReadDto.cs            # DTO para envio externo
│
├── Cli
│   └── CliRunner.cs             # Execução via linha de comando
│
└── Form1.cs                     # Interface gráfica (WinForms)

🖥️ Interface Gráfica (WinForms)

A aplicação gráfica permite:
Abertura automática de todos os leitores RFID conectados
Definição de meta global de tags únicas
Visualização em tempo real das leituras

Grid detalhado contendo:
Leitor responsável
Antena
RSSI
Quantidade de leituras por tag
Primeira e última leitura
Cópia rápida do EPC com duplo clique
Exportação completa para XML
Logs detalhados e performáticos

🔄 Fluxo básico (GUI)

Conectar os leitores RFID via USB
Abrir a aplicação
Clicar em Open
Definir a meta de tags únicas
Clicar em Start
Acompanhar leituras em tempo real
Exportar XML ou encerrar o ciclo

🧾 Exportação de Dados (XML)
O sistema permite exportar todas as informações coletadas para um arquivo XML estruturado, contendo:
Metadados do ciclo de leitura
Leitores utilizados
Tags lidas (com métricas completas)
Log completo da execução (opcional)

📄 Exemplo de estrutura XML
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

⚙️ Modo CLI (Linha de Comando)

O projeto também pode ser executado sem interface gráfica, ideal para automação, testes ou integração em pipelines.

▶️ Exemplo de uso
RfidRastroVerde.exe 100 80

📌 Parâmetros

100 → Meta global de tags únicas
80 → Intervalo de polling em milissegundos

Funcionalidades no CLI:
Detecção automática de leitores conectados
Exibição de leituras em tempo real
Progresso global de tags únicas
Encerramento automático ao atingir a meta
Parada manual pressionando Q

🌐 Integração com API (Opcional)

O sistema suporta envio assíncrono das leituras para uma API REST.

Como funciona:
Cada tag única gera um DTO (TagReadDto)
Os dados são enviados por uma fila interna
O envio não bloqueia leitura nem UI
A API pode ser habilitada ou desabilitada por configuração

⚙️ Exemplo de configuração
{
  "BaseUrl": "https://api.exemplo.com",
  "DeviceId": "RFID-01"
}


📌 Se BaseUrl estiver vazia ou Enabled = false, a API é automaticamente desativada.

🧩 Requisitos Técnicos

Windows
.NET Framework / .NET compatível com WinForms
Leitores RFID HID compatíveis com SWHidApi.dll
Visual Studio 2022 (recomendado)

🛠️ Observações Importantes

O sistema sempre reinicia o estado ao iniciar um novo ciclo
Não há reaproveitamento de leituras anteriores
Funciona corretamente com 1 ou múltiplos leitores
Leituras duplicadas entre leitores são tratadas corretamente
Projetado para ambientes industriais e operação contínua

📌 Status do Projeto

🟢 Ativo
🛠️ Em evolução contínua
📈 Arquitetura preparada para expansão:

novos protocolos
banco de dados
dashboards
integrações industriais

👤 Autor

Nicolas Zapelão
Projeto: RfidRastroVerde

📄 Licença

Este projeto pode ser licenciado conforme a necessidade do cliente ou uso interno.
Entre em contato para mais informações.