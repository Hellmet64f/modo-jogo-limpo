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
- **REPARAR** (separador ESTADO) volta a restaurar tudo, mesmo que algo tenha
  ficado para tras.

## Separadores

| Separador | Para que serve |
|---|---|
| PAINEL | Liga/desliga, medidores RAM e CPU, contador de processos ao vivo, registo em tempo real |
| PERFIS | Nuvem (GeForce Now) / Leve / Personalizado, categoria por categoria, lista de programas a nao fechar |
| ESTADO | O que foi desligado (lido do snapshot) e o que o sistema esta a fazer agora, com botao REPARAR |
| AJUSTES | Pasta do app, wallpaper, manutencao e sobre |

## Como garantir que volta tudo

O script grava um **snapshot** (`%LOCALAPPDATA%\ModoJogoLimpo\snapshot.json`) com o
estado anterior de tudo: servicos (e o seu tipo de arranque), tarefas agendadas,
chaves de registo, plano de energia, exclusoes do Defender, itens de
inicializacao e atalhos da area de trabalho.

O MODO NORMAL le esse snapshot e desfaz item a item. Se o snapshot nao puder ser
lido, **nada e alterado**.

`-DryRun` imprime o plano completo sem tocar em nada - use para ver o que
seria feito.

## Ficheiros

```
ModoJogo.exe     aplicativo e interface
ModoJogo.ps1     o motor (snapshot, perfis, restauro)
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

## Estado do sistema

O script **nao toca** em: driver de video, audio, rede, Explorador, anti-cheat,
o proprio app, nem no launcher do jogo. Os servicos termicos do Acer
(Care Center e Experience Zone) tambem ficam de fora.
