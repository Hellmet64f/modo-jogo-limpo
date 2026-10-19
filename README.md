<div align="center">

# 🎮 Modo Jogo Limpo

### O teu PC como uma sessão de cloud gaming — e volta **exatamente** ao que estava.

[![Beta](https://img.shields.io/badge/estado-BETA%20%F0%9F%9A%80-ff6b6b?style=for-the-badge)](https://github.com/Hellmet64f/modo-jogo-limpo/releases)
[![Release](https://img.shields.io/badge/download-v4.0-8b5cf6?style=for-the-badge&logo=github)](https://github.com/Hellmet64f/modo-jogo-limpo/releases/latest)
[![Site](https://img.shields.io/badge/site-gh--pages-22d3ee?style=for-the-badge&logo=githubpages)](https://hellmet64f.github.io/modo-jogo-limpo/)
[![Licença](https://img.shields.io/badge/licen%C3%A7a-MIT-8b5cf6?style=for-the-badge)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078d4?style=for-the-badge)]()
[![Código aberto](https://img.shields.io/badge/c%C3%B3digo-aberto-2ea44f?style=for-the-badge)]()
[![C#](https://img.shields.io/badge/interface-C%23-239120?style=for-the-badge)]()
[![PowerShell](https://img.shields.io/badge/motor-PowerShell-012456?style=for-the-badge)]()

</div>

---

> ### ⚠️ Isto está em **BETA**
> Funciona e foi testado em vários PCs, mas **pode ter bugs**. Testa sempre
> antes de uma sessão longa. Grava o teu trabalho antes de activar.
> Os erros que encontrares são bem-vindos — são o que falta para a v1.

---

## ⬇️ Descarregar

<div align="center">

[![Descarregar v4.0](https://img.shields.io/badge/%E2%9A%99%20Descarregar%20v4.0-8b5cf6?style=for-the-badge&logo=github&logoColor=white)](https://github.com/Hellmet64f/modo-jogo-limpo/releases/latest/download/ModoJogoLimpo-v4.0.zip)

**79 KB** · Windows 10/11 · requer administrador · [ver todas as versões](https://github.com/Hellmet64f/modo-jogo-limpo/releases)

[**🌐 Abrir o site do projeto**](https://hellmet64f.github.io/modo-jogo-limpo/)

</div>

---

## O que é isto?

O [GeForce Now](https://www.nvidia.com/pt-pt/geforce-now/) e o
[Blacknut](https://www.blacknut.com/) correm o teu jogo num ambiente limpo,
isolado do teu PC. O **Modo Jogo Limpo** faz a mesma coisa **localmente**:

> 🧹 **Fecha** tudo o que não é jogo
> 🙈 **Esconde** a interface do Windows 11
> ⏸️ **Pausa** as actualizações e a recolha de dados
> 🧠 **Liberta** a RAM e o CPU para o jogo
> ↩️ **Desfaz tudo**, ao milímetro, quando quiseres

```text
                         ANTES          MODO JOGO      DEPOIS DE VOLTAR
Processos               240      →      148      →      volta ao original
Serviços a correr       126      →       85      →      volta ao original
Tarefas agendadas       144      →      142      →      volta ao original
RAM livre             2,2 GB     →    2,9 GB     →      volta ao original
Plano de energia   Equilibrado  →  Desem. Máx.  →  Equilibrado (o teu!)
```

---

## 🖼️ As quatro páginas

| Painel | Perfis | Estado | Ajustes |
|:---:|:---:|:---:|:---:|
| ![Painel](docs/01-painel.png) | ![Perfis](docs/02-perfis.png) | ![Estado](docs/03-estado.png) | ![Ajustes](docs/04-ajustes.png) |

---

## 🔓 100% de controlo — nada é uma caixa negra

Este aplicativo **nunca te pede confiança**. Espera que acredites.

- **Vês tudo o que vai ser desligado** antes de acontecer, na página *Estado*.
- **Cada alteração é reversível** — e o reverso é exacto, não aproximado.
- **Tens o snapshot**: antes de mexer em alguma coisa, o aplicativo grava o
  estado anterior de *tudo* — cada serviço (e o seu tipo de arranque), cada
  tarefa agendada, cada chave de registo, o plano de energia, as exclusões do
  antivírus, os itens de inicialização e os atalhos. O "modo normal" lê esse
  ficheiro e desfaz item a item.
- **Se o snapshot não puder ser lido, nada é alterado.** Falha segura.
- **`-DryRun`**: mostra exactamente o que ia ser feito, sem tocar em nada.
- **Perfil personalizado**: escolhes categoria por categoria o que desliga.
- **O código está todo aberto**, nas tuas mãos, para leres e mudares como
  quiseres.

### 📂 Onde fica o snapshot

```
%LOCALAPPDATA%\ModoJogoLimpo\snapshot.json
```

É um ficheiro de texto. Podes abrir, ler e até editar à mão.

---

## 🧠 O motor: um snapshot antes de tudo

A maioria dos "modos jogo" da internet desligam coisas e **nunca mais ligam**.
Este faz o inverso.

```
   snapshot.json  ──►  desligar tudo  ──►  jogar  ──►  snapshot.json  ──►  tudo como estava
```

O mesmo snapshot protege o teu **anti-cheat**: se o modo jogo desligar um
serviço de anti-cheat, ele volta exactamente como estava.

---

## ✨ Funcionalidades

### ☁️ Três perfis

| Perfil | O que faz |
|---|---|
| ☁️ **Nuvem** | Desliga tudo. Só o jogo, os launchers e o sistema mínimo. O mais leve. |
| 🎮 **Leve** | Mantém Discord, WhatsApp e o navegador. Não mexe no Update. |
| ⚙️ **Personalizado** | Escolhes categoria por categoria (11 disponíveis). |

### 📦 As 11 categorias

| Categoria | O que desliga |
|---|---|
| **Interface do Windows 11** | barra de tarefas escondida, menu Início, barra de pesquisa, widgets, chat, Cortana, notificações |
| **Telemetria e recolha de dados** | 34 serviços de recolha, tarefas agendadas de compatibilidade, política de telemetria a `0` |
| **Windows Update** | pausado durante a sessão (volta ao estado original depois) |
| **Indexação e busca** | o Windows Search e o indexador de ficheiros |
| **Gravação de tela** | Game DVR desligado — deixa de gravar em segundo plano |
| **Desempenho e energia** | plano "Desempenho Máximo", CPU mínima a 100%, sem suspensão USB nem PCIe |
| **Programas que voltam sozinhos** | 59 programas bloqueados (Edge, OneDrive, Copilot, widgets, bloatware…) |
| **Itens de inicialização** | adiados até ao fim da sessão |
| **Antivírus** | exclusões para o Epic e o Steam |
| **Área de trabalho** | fica só o ícone do app, o Epic, o Steam e os teus jogos |
| **Processos e serviços de fundo** | fecha o que não é jogo |

### 🛡️ O vigilante — fica a trabalhar em segundo plano

O Windows relança sozinho vários programas (a pesquisa, os widgets, o painel de
notícias do Início, os assistants, o OneDrive, o browser). O **vigilante** é um
temporizador dentro do aplicativo que, **de 2 em 2 segundos**:

- fecha a **pesquisa** do Windows (é uma app UWP — o Windows volta a
  arrancá-la, sem o vigilante ela volta a aparecer e a comer ~160 MB)
- fecha os **programas que se autolançam**
- volta a **esconder a barra de tarefas** quando o Windows a mostra

Corre **mesmo com o aplicativo minimizado** — fica lá a trabalhar enquanto
jogas. Liga e desliga na página *Perfis*.

### 🔒 O que NUNCA é tocado

Estas regras estão no código e são a razão de o aplicativo não partir o teu PC:

- **O WebView2** (`msedgewebview2.exe`) — é o motor de interface da Epic, do
  Roblox e da própria pesquisa do Windows 11. Bloquear ou matar o WebView2
  deixa a janela **preta** e a aplicação não funciona. *(Este bug aconteceu,
  foi diagnosticado e corrigido — vê o histórico do git.)*
- **O driver de vídeo, o áudio e a rede**
- **O Explorador** (é ele que desenha o wallpaper)
- **O anti-cheat** — Easy Anti-Cheat, BattlEye, GameGuard, XignCode, nProtect e
  Valve Anti-Cheat nunca são fechados nem bloqueados. Se um destes morrer, o
  jogo dá erro e não arranca.
- **Os serviços térmicos do Acer** (Care Center e Experience Zone)
- **O TokenBroker** — é o login Microsoft usado pela Epic e pela Store

---

## ⌨️ Atalhos

| Atalho | Acção |
|---|---|
| `Ctrl` + `Alt` + `J` | Activar o modo jogo |
| `Ctrl` + `Alt` + `M` | Voltar ao modo normal (emergência) |

O aplicativo **minimiza-se sozinho** quando activa o modo jogo — como está
sempre no topo, tapava a janela do launcher e do jogo.

---

## 📥 Instalar

1. Descarrega o [`ModoJogo.exe`](ModoJogo.exe) para uma pasta tua
   (por exemplo `C:\JogoLimpo`).
2. Cria um atalho no ambiente de trabalho.
3. Executa. Pede autorização de administrador (é preciso para mexer em
   serviços, tarefas e registo).

Funciona a partir de **qualquer pasta** — encontra o script e os wallpapers ao
lado de si. Para mudar de pasta: botão **"Mudar pasta..."** na página *Ajustes*.

### Wallpapers

Coloca as tuas imagens na pasta `wallpapers\`. O aplicativo usa-as todas: o
modo jogo aplica a que escolheres e a página *Ajustes* mostra as duas
primeiras. Sem imagens, funciona na mesma (mantém o wallpaper do Windows).

---

## 🔧 Compilar a partir do código

Precisas do **.NET Framework 4.x** (o `csc.exe` já vem com ele).

```cmd
build.cmd
```

Compila os `.cs` da pasta `app\` para `ModoJogo.exe`, com o ícone e o manifesto
(que pede administrador e ativa o DPI por monitor).

---

## 📁 Estrutura

```
ModoJogo.exe      aplicativo e interface (C#, .NET Framework)
ModoJogo.ps1      o motor: snapshot, perfis, restauro, pesquisa
build.cmd         compila os .cs para o .exe
app\              código-fonte C#
  ModoJogo.cs       janela principal, páginas, vigilante
  UI.cs             paleta, cartões, botões, controlos desenhados à mão
  SeletorWallpaper.cs
  GerarIcone.cs     gera o ícone do aplicativo
  app.manifest
wallpapers\      as tuas imagens
docs\            capturas de ecrã
```

---

## ⚙️ Linha de comandos

```powershell
.\ModoJogo.ps1 -Acao Ativar    -Perfil nuvem -DryRun   # mostra o plano, não toca em nada
.\ModoJogo.ps1 -Acao Ativar    -Perfil leve
.\ModoJogo.ps1 -Acao Ativar    -Perfil personalizado -Categorias "interface,telemetria,processos"
.\ModoJogo.ps1 -Acao Desativar
.\ModoJogo.ps1 -Acao Busca     -EstadoBusca on         # desactiva a pesquisa
.\ModoJogo.ps1 -Acao Estado
```

---

## 🤝 Ajudar o projeto original

O código é teu para usares, estudares e modificares à vontade — **faz a tua
versão** sem qualquer obrigação.

Mas se quiseres que o **projeto original** melhore, o caminho é:

1. **Abre uma issue** a descrever o que encontraste
2. **Manda um pull request** com a correcção
3. **Reporta um bug** com o que reproduziste — isso vale tanto quanto uma
   correcção

O historial do git conta a história toda: cada bug que foi diagnosticado e
corrigido está lá, com a explicação do porque.

---

## 📌 Notas honestas

- **A caixa "Pesquisar" da barra de tarefas** no Windows 11 24H2 é guardada pelo
  shell num sítio que o registo não controla. O botão *"Definições da barra de
  tarefas..."* abre a página certa das Definições do Windows (um clique).
  Durante o modo jogo isso não se vê, porque a barra está escondida.
- **O "modo normal" não reabre as aplicações que fechou** (Edge, WebView,
  Copilot…). É o comportamento de um modo jogo a sério. A lista *"Não fechar
  estes programas"* deixa-te poupar as que usares.
- **O antivírus com protecção em tempo real desactivado** é uma escolha, não um
  esquecimento: ganha-se FPS mas abre-se uma janela de segurança durante a
  partida. As exclusões para o Epic e o Steam são sempre aplicadas.
- Atinge-se ~148 processos num PC com 8 GB. Chegar a 60-80 exigiria desligar
  serviços do sistema **em todo o arranque** (compartilhamento de `svchost`),
  o que obriga a reiniciar e traz risco de o Windows não arrancar. Não está
  incluído de propósito.
- **Beta.** Testa antes de confiar.

---

## 📄 Licença

[MIT](LICENSE) — usa, copia, modifica e distribui à vontade.

🌐 **Site do projeto:** https://hellmet64f.github.io/modo-jogo-limpo/

Feito com **[OpenCode](https://opencode.ai)**.

