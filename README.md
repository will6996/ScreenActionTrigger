# Screen Action Trigger

Plataforma profissional de automação visual para Windows.

## Visão Geral

Screen Action Trigger monitora regiões da tela em tempo real, detecta padrões visuais (cor, mudança ou template) e executa ações automatizadas de mouse/teclado com base em regras configuráveis pelo usuário.

---

## Pré-requisitos

| Item | Versão |
|------|--------|
| Windows | 10 / 11 (x64) |
| .NET SDK | 8.0 ou superior |
| Visual Studio | 2022 17.8+ |

---

## Compilação

```bash
# Restaurar pacotes e compilar (desenvolvimento)
dotnet restore
dotnet build -c Release

# Executar em modo dev
dotnet run --project ScreenActionTrigger.UI -c Release

# Testes
dotnet test ScreenActionTrigger.Tests -c Release --logger "console;verbosity=detailed"
```

## Distribuição (executável único)

Para gerar **um único `.exe`** que roda sem pastas, DLLs ou runtime instalado:

```powershell
.\build.ps1 -publish sc
```

Saída: `publish\self-contained\ScreenActionTrigger.exe` (~165 MB)

Copie esse arquivo para a Área de Trabalho e crie um atalho — pronto.

Perfis, templates e logs ficam em `%APPDATA%\ScreenActionTrigger\` (não ao lado do `.exe`).

## Atualizações automáticas

O app verifica novas versões no GitHub Releases (aba **Atualizações**).
Para publicar uma nova versão:

```powershell
# 1. Atualize <Version> no ScreenActionTrigger.UI.csproj
# 2. Execute:
.\release.ps1 -Notes "Descrição das mudanças"
```

---

## Arquitetura

```
ScreenActionTrigger.sln
├── ScreenActionTrigger.Core          # Modelos, interfaces, RuleEngine
├── ScreenActionTrigger.Vision        # OpenCvSharp, captura, detectores
├── ScreenActionTrigger.Input         # Win32 SendInput, mouse/teclado
├── ScreenActionTrigger.Persistence   # System.Text.Json, perfis
├── ScreenActionTrigger.Overlay       # WPF transparente click-through
├── ScreenActionTrigger.UI            # WPF MVVM, interface principal
└── ScreenActionTrigger.Tests         # xUnit, Moq, FluentAssertions
```

---

## Funcionalidades

### Regiões Monitoradas
- Criar/renomear/redimensionar/mover regiões ilimitadas
- Ativar/desativar individualmente
- Organizar em grupos
- Seleção interativa na tela (arrastar para definir)

### Detecção Visual (3 modos)
| Modo | Descrição |
|------|-----------|
| **Cor** | Detecta cor-alvo com tolerância e percentual mínimo |
| **Mudança** | Compara frame atual com anterior (sensibilidade configurável) |
| **Template** | OpenCV `MatchTemplate` com TM_CCOEFF_NORMED, TM_CCORR_NORMED, TM_SQDIFF_NORMED; escala automática |

### Motor de Regras
- Condições compostas: **AND** / **OR** / **NOT**
- Prioridade, cooldown e limite máximo de execuções por regra

### Ações Suportadas
- Mouse: clique esquerdo/direito/duplo, pressionar/soltar, scroll
- Teclado: pressionar tecla, combinação, segurar, soltar
- Sistema: executar comando, tocar som, exibir notificação

### Biblioteca de Templates
- Categorias: Interface / Combate / Recursos / Eventos / Personalizados
- Importar PNG/JPG/BMP, capturar diretamente da tela
- Exportar, duplicar, excluir

### Overlay Transparente
- Janela WPF click-through, sempre no topo
- Exibe regiões monitoradas e detecções em tempo real

### Painel de Monitoramento
- Log em tempo real com filtros
- Estatísticas: total detecções, execuções, erros, confiança média
- Exportar log em CSV

### Persistência (JSON)
- Salvar/carregar/importar/exportar perfis `.satprofile`
- Histórico dos 10 perfis recentes

---

## Stack Tecnológica

- **C# 13 / .NET 8** — target `net8.0-windows`
- **WPF + MVVM** — CommunityToolkit.Mvvm 8.x
- **OpenCvSharp4** — Template matching
- **Win32 SendInput** — P/Invoke para mouse e teclado
- **System.Text.Json** — Serialização de perfis
- **Microsoft.Extensions.DependencyInjection** — IoC container
- **Microsoft.Extensions.Logging** — Logging estruturado
- **xUnit + Moq + FluentAssertions** — Testes unitários

---

## Estrutura dos Testes

```
ScreenActionTrigger.Tests/
├── RuleEngineTests.cs       (12 testes)
├── TemplateMatcherTests.cs  ( 7 testes)
├── ColorDetectorTests.cs    ( 6 testes)
├── ChangeDetectorTests.cs   ( 6 testes)
├── ActionDispatcherTests.cs ( 7 testes)
├── ProfileManagerTests.cs   (10 testes)
└── VisionEngineTests.cs     ( 9 testes)
```

---

## Atalhos Padrão

| Atalho | Ação |
|--------|------|
| `F9`   | Iniciar / Parar monitoramento |
| `F10`  | Pausar / Continuar |

---

## Licença

MIT — Uso livre para fins pessoais e comerciais.
