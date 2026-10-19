# MODO JOGO LIMPO (v4)

Deixa o PC como uma sessao de cloud gaming: so o jogo, os launchers e o minimo
do sistema a correr. E volta **exatamente** ao estado anterior.

## Como usar

- **MODO JOGO** no separador PAINEL: fecha o que nao e jogo, pausa o Windows
  Update, limpa a interface do Windows 11 e esconde tudo da area de trabalho
  menos o app, o Epic, o Steam e os jogos.
- **MODO NORMAL**: devolve tudo. O botao esta sempre disponivel depois de uma
  accao (antes ficava morto - bug corrigido).
- Atalho de emergencia: **Ctrl+Alt+M** volta ao normal a qualquer momento.
  **Ctrl+Alt+G** ativa o modo jogo.

## Separadores

| Separador | Para que serve |
|---|---|
| PAINEL | Liga/desliga, medidores RAM e CPU, contador de processos ao vivo, registo em tempo real |
| PERFIS | Nuvem (GeForce Now) / Leve / Personalizado, categoria por categoria, lista de programas a nao fechar |
| ESTADO | O que foi desligado (lido do snapshot) e o que o sistema esta a fazer agora, com botao REPARAR |
| AJUSTES | Pasta do app, wallpaper, **pesquisa do Windows** e manutencao |

## A pesquisa do Windows

O botao **DESATIVAR A PESQUISA** na pagina AJUSTES:

- esconde a caixa "Pesquisar" da barra de tarefas
- bloqueia o SearchHost, o SearchApp e o indexador (nunca mais arrancam)
- desliga a Cortana, a pesquisa na web e a pesquisa na nuvem
- o app mantem um vigilante que fecha a pesquisa de 2 em 2 segundos, porque o
  Windows e um app UWP e volta a arranca-lo sozinho (poupa 75 a 160 MB)

**Nota honesta:** no Windows 11 24H2 a caixa "Pesquisar" da barra de tarefas e
guardada pelo shell num sitio que o registo nao controla. Por isso o botao
**Definicoes da barra de tarefas...** abre a pagina certa do Windows
(Definicoes > Barra de tarefas > Itens da barra de tarefas > Pesquisar >
Desligar) - e o unico jeito garantido de a tirar de vista. Durante o MODO JOGO
isso nao se ve porque a barra de tarefas esta escondida.

## Como garantir que volta tudo

O script grava um **snapshot** (`%LOCALAPPDATA%\ModoJogoLimpo\snapshot.json`) com o
estado anterior de tudo: servicos (e o seu tipo de arranque), tarefas agendadas,
chaves de registo, plano de energia, exclusoes do Defender, itens de
inicializacao e atalhos da area de trabalho.

O MODO NORMAL le esse snapshot e desfaz item a item. Se o snapshot nao puder ser
lido, **nada e alterado**.

`-DryRun` imprime o plano completo sem tocar em nada - use para ver o que
seria feito.

## Regras que nunca sao quebradas

O script **nao toca** em:

- o **WebView2** (`msedgewebview2.exe`) - e o motor de interface da Epic, do
  Roblox e da propria pesquisa do Windows. Bloquear ou matar o WebView2 deixa a
  janela da Epic **preta** e a app nao funciona. (Ja aconteceu, esta corrigido.)
- o driver de video, o audio e a rede
- o Explorador (e ele que desenha o wallpaper)
- o anti-cheat, o launcher e o jogo
- os servicos termicos do Acer (Care Center e Experience Zone)
- o TokenBroker, que e o login Microsoft usado pela Epic e pela Store

## Ficheiros

```
ModoJogo.exe     aplicativo e interface
ModoJogo.ps1     o motor (snapshot, perfis, restauro, pesquisa)
build.cmd        recompila os .cs para o .exe
app\             os codigos .cs que geraram o EXE
wallpapers\      as imagens
```

## Compilar

```
build.cmd
```

Precisa do .NET Framework 4.x (o `csc.exe` ja vem com ele).

## Perfis

- **NUVEM** - desliga tudo. So o jogo, os launchers e o sistema minimo.
- **LEVE** - mantem Discord, WhatsApp e o navegador; nao mexe no Update.
- **PERSONALIZADO** - escolhe categoria por categoria.

## Atalho de linha de comandos

```
ModoJogo.exe --pagina=perfis     abre direto num separador
```

## Novidades v4.1

- **O MODO JOGO ja nao abre dialogo de wallpaper.** Usa o wallpaper guardado.
  Para mudar, vai a pagina AJUSTES.
- **A app minimiza-se sozinha** quando activa o modo jogo: como e sempre no
  topo, ficava a tapar a janela do launcher e do jogo. `Ctrl+Alt+G` ou
  `Ctrl+Alt+M` trazem a app de volta.
- **Vigilante**: um temporizador dentro do app fecha, de 2 em 2 segundos, os
  programas que o Windows volta a abrir sozinho (widgets, painel de noticias
  do Inicio, assistants, OneDrive, browser, bloatware Acer) e volta a esconder
  a barra de tarefas. Corre mesmo com a app minimizada. Liga/desliga na
  pagina PERFIS.
- **Anti-cheat sempre protegido**: Easy Anti-Cheat, BattlEye, GameGuard,
  XignCode, nProtect e Valve Anti-Cheat nunca sao fechados nem bloqueados.
  Se um destes for morto, o jogo da erro e nao arranca.
