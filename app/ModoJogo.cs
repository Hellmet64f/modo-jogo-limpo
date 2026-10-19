using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModoJogoApp
{
    public class MainForm : Form
    {
        // ---------- infra ----------
        private const string DirPadrao = @"C:\Users\migue\JogoLimpo";
        public static string DirBase = DirPadrao;

        private static readonly string EstadoDir =
            Environment.GetEnvironmentVariable("LOCALAPPDATA") + @"\ModoJogoLimpo";

        // ---------- coracao ----------
        private string arquivoEstado, arquivoResultado, arquivoSnapshot, arquivoWallpadrao;
        private string pastaWallpapers, scriptCaminho, arquivoSaida;
        private string wallSelecionado = "";
        private bool rodando = false;

        // ---------- perfil ----------
        private string perfil = "nuvem";
        private bool[] catAtivas;
        // Por omissao protege as ferramentas de desenvolvimento: fechar o
        // editor a meio do trabalho perde o que estava a fazer. O campo
        // "NAO FECHAR ESTES PROGRAMAS" na pagina PERFIS e editavel.
        private string manterProcessos = "OpenCode,Code,RobloxStudio,devenv,obs64,StudioMCP";
        private bool opcaoVigiarBarra = true;
        private Caixa caixaVigilante;
        private bool opcaoVigilante = true;
        private bool opcaoConfirmar = true;
        private Caixa caixaConfirmar;

        // ---------- contadores ----------
        private int processosAntes = 0;
        private int processosAgora = 0;
        private double ramAntes = 0;

        // ---------- UI ----------
        private FundoGradiente fundo;
        private Panel barraLateral, area;
        private ItemNav navPainel, navPerfis, navDiag, navAjustes;
        private Label lblTitulo, lblSub, lblStatus, lblDica;
        private Panel pagPainel, pagPerfis, pagDiag, pagAjustes;
        private Medidor gMedidorRam, gMedidorCpu;
        private Etiqueta tagModo;
        private TextBox txtLog, txtDiag, txtManter;
        private Label lblProcessos, lblProcessosDelta;
        private Cartao cartaoJogo, cartaoNormal, cartaoRecursos, cartaoPasta, cartaoStatus, cartaoBloco;
        private Cartao[] cartoesPerfil;
        private Caixa[] caixas;
        private Botao btnReparar, btnBusca;
        private Botao btnBuscaDefs;
        private Label lblBusca;
        private PerformanceCounter pcCpu = null;
        private Microsoft.VisualBasic.Devices.ComputerInfo info = new Microsoft.VisualBasic.Devices.ComputerInfo();

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Timer timerExec;
        private System.Windows.Forms.Timer timerBusca;
        private Process procExec;
        private int linhasLidas = 0;
        private DateTime inicioExec;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h, int id, uint m, uint k);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string classe, string titulo);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr raiz, out IntPtr guidAtivo);

        private class HotkeyWindow : NativeWindow
        {
            public MainForm P;
            public HotkeyWindow(MainForm p)
            { P = p; CreateHandle(new CreateParams()); }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1) { P.RestaurarApp(); P.Executar("Desativar"); }
                if (m.Msg == 0x0312 && m.WParam.ToInt32() == 2) { P.RestaurarApp(); P.Executar("Ativar"); }
                base.WndProc(ref m);
            }
        }
        private HotkeyWindow hk;

        // ---------- categorias ----------
        private static readonly string[] CatIds = {
            "interface","telemetria","update","indexacao","gravacao","desempenho",
            "apps","inicializacao","defender","desktop","processos"
        };
        private static readonly string[] CatTexto = {
            "Interface do Windows 11",
            "Telemetria e coleta de dados",
            "Pausar o Windows Update",
            "Indexacao e busca de ficheiros",
            "Gravacao de tela (Game DVR)",
            "Desempenho e energia",
            "Bloquear programas que voltam sozinhos",
            "Itens de inicializacao",
            "Antivirus (exclusoes e tempo real)",
            "Limpar a area de trabalho",
            "Fechar programas e servicos de fundo"
        };
        private static readonly string[] CatDetalhe = {
            "barra, Inicio, busca, widgets, chat",
            "31 servicos de coleta + tarefas agendadas",
            "para de baixar/instalar durante a partida",
            "Windows Search e o indexador de ficheiros",
            "deixa de gravar a tela em segundo plano",
            "plano Desempenho Maximo, CPU a 100%",
            "Edge, OneDrive, Copilot, bloatware Acer",
            "adia o que arranca sozinho com o Windows",
            "libera Epic e Steam da varredura",
            "esconde tudo menos o app, Epic e os jogos",
            "fecha apps de terceiros e servicos de fundo"
        };

        // =================================================================
        //  CONSTRUTOR
        // =================================================================
        public MainForm()
        {
            string cfg = Path.Combine(EstadoDir, "pasta.txt");
            if (File.Exists(cfg))
            {
                try { string p = File.ReadAllText(cfg).Trim(); if (Directory.Exists(p)) DirBase = p; }
                catch { }
            }

            arquivoEstado     = Path.Combine(EstadoDir, "ativo.txt");
            arquivoResultado  = Path.Combine(EstadoDir, "resultado.txt");
            arquivoSnapshot   = Path.Combine(EstadoDir, "snapshot.json");
            arquivoWallpadrao = Path.Combine(EstadoDir, "wallpadrao.txt");
            arquivoSaida      = Path.Combine(EstadoDir, "ultima-saida.txt");
            pastaWallpapers   = Path.Combine(DirBase, "wallpapers");
            scriptCaminho     = Path.Combine(DirBase, "ModoJogo.ps1");

            catAtivas = new bool[CatIds.Length];
            for (int i = 0; i < CatIds.Length; i++) catAtivas[i] = true;

            // O wallpaper escolhido era gravado mas NUNCA era lido de volta:
            // o app voltava sempre ao jogo.jpg.
            wallSelecionado = Path.Combine(pastaWallpapers, "jogo.jpg");
            if (File.Exists(arquivoWallpadrao))
            {
                try
                {
                    string w = File.ReadAllText(arquivoWallpadrao).Trim();
                    if (!string.IsNullOrEmpty(w) && File.Exists(w)) wallSelecionado = w;
                }
                catch { }
            }

            Text = "Modo Jogo Limpo";
            ClientSize = new Size(1160, 770);
            MinimumSize = new Size(1000, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Paleta.FundoA;
            ForeColor = Paleta.Texto;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.None;

            Construir();
            IrPara("painel");
            // Permite abrir direto numa pagina:  ModoJogo.exe --pagina=perfis
            string pagina = "";
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i].StartsWith("--pagina=", StringComparison.OrdinalIgnoreCase))
                        pagina = args[i].Substring(9).ToLower();
                }
            }
            catch { }
            if (pagina == "perfis" || pagina == "estado" || pagina == "ajustes" || pagina == "painel")
                IrPara(pagina == "estado" ? "diag" : pagina);

            timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (s, e) => Medir();
            timer.Start();

            timerExec = new System.Windows.Forms.Timer { Interval = 400 };
            timerExec.Tick += (s, e) => Acompanhar();

            // Vigia a pesquisa do Windows. O SearchHost e um app UWP: o IFEO
            // nao se aplica a ele e o shell relanca-o sempre. Este temporizador
            // garante que nunca volta a acumular os ~160 MB.
            timerBusca = new System.Windows.Forms.Timer { Interval = 2000 };
            timerBusca.Tick += (s, e) => VigiarPesquisa();
            timerBusca.Start();

            hk = new HotkeyWindow(this);
            // Ctrl+Alt+M volta ao normal. Ctrl+Alt+G ativa o modo jogo.
            // Ctrl+Alt+M volta ao normal (emergencia). Ctrl+Alt+J ativa o modo jogo.
            // O G foi evitado: colidia com atalhos de outras aplicacoes.
            if (!RegisterHotKey(hk.Handle, 1, 0x0002 | 0x0001 | 0x4000, (uint)Keys.M))
                Anexar("ATENCAO: o atalho Ctrl+Alt+M nao foi registado (outra app usa-o)");
            if (!RegisterHotKey(hk.Handle, 2, 0x0002 | 0x0001 | 0x4000, (uint)Keys.J))
                Anexar("ATENCAO: o atalho Ctrl+Alt+J nao foi registado (outra app usa-o)");
            RegisterHotKey(hk.Handle, 2, 0x0002 | 0x0001 | 0x4000, (uint)Keys.J);
            FormClosed += (s, e) =>
            {
                UnregisterHotKey(hk.Handle, 1);
                UnregisterHotKey(hk.Handle, 2);
                timer.Stop();
                if (timerExec != null) timerExec.Stop();
            };

            // O estado SÓ era lido depois de uma acao: ao abrir, a etiqueta
            // dizia sempre "MODO NORMAL" mesmo com o modo jogo ativo.
            AtualizarEstado();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SetWindowPos(this.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0003);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // O Windows lembra o estado da janela: se a app ficou minimizada
            // no ultimo MODO JOGO, arrancava minimizada e o utilizador via
            // apenas uma linha na barra de tarefas.
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            // Centra a janela no ecra principal usando os limites REAIS.
            // O StartPosition=CenterScreen calculava mal quando a area de
            // trabalho e menor do que a janela e deixava-a fora do ecra.
            var s = Screen.PrimaryScreen;
            if (s != null)
            {
                var wa = s.WorkingArea;
                int x = wa.Left + (wa.Width - Width) / 2;
                int y = wa.Top + (wa.Height - Height) / 2;
                if (x < wa.Left) x = wa.Left;
                if (y < wa.Top) y = wa.Top;
                Location = new Point(x, y);
            }
        }

        // =================================================================
        //  MONTAGEM DA INTERFACE
        // =================================================================
        private void Construir()
        {
            fundo = new FundoGradiente { Dock = DockStyle.Fill };
            Controls.Add(fundo);

            // IMPORTANTE: a area (Dock=Fill) entra ANTES da barra lateral.
            area = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            fundo.Controls.Add(area);

            barraLateral = new Panel { Dock = DockStyle.Left, Width = 86, BackColor = Color.FromArgb(14, 12, 32) };
            fundo.Controls.Add(barraLateral);

            var logo = new Panel { Location = new Point(0, 10), Size = new Size(86, 74), BackColor = Color.Transparent };
            logo.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(24, 14, 38, 38);
                using (var br = new LinearGradientBrush(r, Paleta.Roxo, Paleta.Azul, LinearGradientMode.ForwardDiagonal))
                    g.FillEllipse(br, r);
                using (var f = new Font("Segoe UI", 15F, FontStyle.Bold))
                using (var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("M", f, Brushes.White, r, st);
            };
            barraLateral.Controls.Add(logo);

            navPainel  = NovoNav("■", "Painel", 96);
            navPerfis  = NovoNav("★", "Perfis", 156);
            navDiag    = NovoNav("▤", "Estado", 216);
            navAjustes = NovoNav("⚙", "Ajustes", 276);
            barraLateral.Controls.AddRange(new Control[] { navPainel, navPerfis, navDiag, navAjustes });

            var lblVer = new Label
            {
                Text = "v4.0",
                ForeColor = Paleta.TextoFraco,
                Font = new Font("Segoe UI", 7.5F),
                AutoSize = true,
                Location = new Point(24, 620),
                BackColor = Color.Transparent
            };
            barraLateral.Controls.Add(lblVer);

            pagPainel  = MontarPaginaPainel();
            pagPerfis  = MontarPaginaPerfis();
            pagDiag    = MontarPaginaDiagnostico();
            pagAjustes = MontarPaginaConfig();
            area.Controls.AddRange(new Control[] { pagAjustes, pagDiag, pagPerfis, pagPainel });
        }

        private ItemNav NovoNav(string ic, string rot, int y)
        {
            var n = new ItemNav { Icone = ic, Rotulo = rot, Location = new Point(11, y), Size = new Size(64, 58) };
            n.Selecionou += (s, e) =>
            {
                if (n.Rotulo == "Painel") IrPara("painel");
                else if (n.Rotulo == "Perfis") IrPara("perfis");
                else if (n.Rotulo == "Estado") IrPara("diag");
                else IrPara("ajustes");
            };
            return n;
        }

        private void IrPara(string qual)
        {
            pagPainel.Visible  = qual == "painel";
            pagPerfis.Visible  = qual == "perfis";
            pagDiag.Visible    = qual == "diag";
            pagAjustes.Visible = qual == "ajustes";
            navPainel.Ativo = qual == "painel";
            navPerfis.Ativo = qual == "perfis";
            navDiag.Ativo = qual == "diag";
            navAjustes.Ativo = qual == "ajustes";
            navPainel.Invalidate(); navPerfis.Invalidate(); navDiag.Invalidate(); navAjustes.Invalidate();

            lblTitulo.Text = qual == "painel" ? "PAINEL"
                           : qual == "perfis" ? "PERFIS"
                           : qual == "diag" ? "ESTADO DO SISTEMA"
                                             : "AJUSTES";
            lblSub.Text = qual == "painel" ? "Sessao limpa estilo GeForce Now"
                         : qual == "perfis" ? "O que o modo jogo desliga"
                         : qual == "diag" ? "O que foi desligado e o que voltou"
                                           : "Pasta, wallpaper e manutencao";
            if (qual == "diag") CarregarDiagnostico();
        }

        // =================================================================
        //  PAGINA PAINEL
        // =================================================================
        private Panel MontarPaginaPainel()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

            lblTitulo = new Label
            {
                Text = "PAINEL", Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                ForeColor = Paleta.Texto, AutoSize = true, Location = new Point(34, 26),
                BackColor = Color.Transparent
            };
            lblSub = new Label
            {
                Text = "Sessao limpa estilo GeForce Now", Font = new Font("Segoe UI", 9.5F),
                ForeColor = Paleta.TextoMudo, AutoSize = true, Location = new Point(36, 62),
                BackColor = Color.Transparent
            };
            tagModo = new Etiqueta { Texto = "MODO NORMAL", Cor = Paleta.Azul, Location = new Point(916, 30), Size = new Size(150, 26) };
            p.Controls.Add(lblTitulo); p.Controls.Add(lblSub); p.Controls.Add(tagModo);

            // --- cartoes de acao ---
            cartaoJogo = new Cartao { Clicavel = true, Location = new Point(34, 100), Size = new Size(238, 190) };
            cartaoJogo.Clicou += (s, e) => AcionarComWallpaperSalvo();
            cartaoNormal = new Cartao { Clicavel = true, Location = new Point(288, 100), Size = new Size(238, 190) };
            cartaoNormal.Clicou += (s, e) => Executar("Desativar");
            DesenharCartaoAcao(cartaoJogo, "MODO JOGO", "Fecha o que nao e jogo,\npausa o Update,\nlimpa a tela",
                               Paleta.Roxo, "▶");
            DesenharCartaoAcao(cartaoNormal, "MODO NORMAL", "Devolve servicos,\ntarefas, registo,\nenergia e icones",
                               Paleta.Azul, "■");

            // --- recursos + contador de processos ---
            cartaoRecursos = new Cartao { Location = new Point(542, 100), Size = new Size(206, 190) };
            cartaoRecursos.Titulo("RECURSOS");
            cartaoRecursos.Sub("medidos ao vivo");
            gMedidorRam = new Medidor { Location = new Point(14, 58), Size = new Size(86, 86), Cor = Paleta.Roxo, Legenda = "RAM" };
            gMedidorCpu = new Medidor { Location = new Point(106, 58), Size = new Size(86, 86), Cor = Paleta.Cyan, Legenda = "CPU" };
            cartaoRecursos.Controls.Add(gMedidorRam);
            cartaoRecursos.Controls.Add(gMedidorCpu);

            lblProcessos = new Label
            {
                Text = "0", Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = Paleta.Cyan, AutoSize = true, Location = new Point(18, 158),
                BackColor = Color.Transparent
            };
            var lblProcTit = new Label
            {
                Text = "PROCESSOS", Font = new Font("Segoe UI", 7F),
                ForeColor = Paleta.TextoFraco, AutoSize = true, Location = new Point(18, 143),
                BackColor = Color.Transparent
            };
            lblProcessosDelta = new Label
            {
                Text = "", Font = new Font("Segoe UI", 7.5F),
                ForeColor = Paleta.Verde, AutoSize = true, Location = new Point(56, 168),
                BackColor = Color.Transparent
            };
            cartaoRecursos.Controls.AddRange(new Control[] { lblProcTit, lblProcessos, lblProcessosDelta });

            // --- caminho do app ---
            cartaoPasta = new Cartao { Location = new Point(764, 100), Size = new Size(272, 190) };
            cartaoPasta.Titulo("INSTALACAO");
            cartaoPasta.Sub("pasta atual do aplicativo");
            var iconePasta = new PictureBox
            {
                Image = Icones.Pasta(28), SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(18, 70), Size = new Size(28, 28), BackColor = Color.Transparent
            };
            var lblPasta = new Label
            {
                Text = DirBase, Font = new Font("Consolas", 8.5F), ForeColor = Paleta.TextoMudo,
                AutoSize = false, Location = new Point(56, 70), Size = new Size(200, 40),
                BackColor = Color.Transparent
            };
            var btnMudar = new Botao { Texto = "Mudar pasta...", Location = new Point(18, 134), Size = new Size(236, 32) };
            btnMudar.Clicou += (s, e) => EscolherPasta();
            cartaoPasta.Controls.AddRange(new Control[] { iconePasta, lblPasta, btnMudar });

            // --- status ---
            cartaoStatus = new Cartao { Location = new Point(34, 306), Size = new Size(1002, 74) };
            var iconeSt = new PictureBox
            {
                Image = Icones.Info(26), SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 23), Size = new Size(26, 26), BackColor = Color.Transparent
            };
            lblStatus = new Label
            {
                Text = "Pronto para ativar o modo jogo.", Font = new Font("Segoe UI", 10.5F),
                ForeColor = Paleta.Texto, AutoSize = false, Location = new Point(58, 16),
                Size = new Size(920, 24), BackColor = Color.Transparent
            };
            lblDica = new Label
            {
                Text = "Atalhos: Ctrl+Alt+G ativa  |  Ctrl+Alt+M volta ao normal a qualquer momento",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoFraco,
                AutoSize = false, Location = new Point(58, 42), Size = new Size(920, 20),
                BackColor = Color.Transparent
            };
            cartaoStatus.Controls.AddRange(new Control[] { iconeSt, lblStatus, lblDica });

            // --- log ---
            cartaoBloco = new Cartao { Location = new Point(34, 396), Size = new Size(1002, 228) };
            cartaoBloco.Titulo("REGISTRO DE ATIVIDADES");
            cartaoBloco.Sub("o que o modo jogo fez, em tempo real");
            txtLog = new TextBox
            {
                Location = new Point(18, 66), Size = new Size(966, 146), Multiline = true,
                ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 13, 34),
                ForeColor = Paleta.TextoMudo, Font = new Font("Consolas", 8.5F), BorderStyle = BorderStyle.None
            };
            cartaoBloco.Controls.Add(txtLog);

            p.Controls.AddRange(new Control[] {
                cartaoRecursos, cartaoJogo, cartaoNormal, cartaoPasta, cartaoStatus, cartaoBloco });
            return p;
        }

        private void DesenharCartaoAcao(Cartao c, string titulo, string desc, Color cor, string glifo)
        {
            c.Controls.Clear();
            c.CorBorda = Color.FromArgb(110, cor);
            c.CorBordaHover = Color.FromArgb(170, cor);
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            p.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new GraphicsPath())
                {
                    br.AddEllipse(-30, -110, 300, 240);
                    using (var gb = new PathGradientBrush(br))
                    {
                        gb.CenterColor = Color.FromArgb(58, cor);
                        gb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                        g.FillPath(gb, br);
                    }
                }
            };
            c.Controls.Add(p);
            p.Enabled = false;   // continua a pintar mas deixa o clique passar

            var ic = new PictureBox
            {
                Image = Icones.Desenhar(glifo, 30, cor), SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 22), Size = new Size(30, 30), BackColor = Color.Transparent
            };
            var l1 = new Label
            {
                Text = titulo, Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = Paleta.Texto, AutoSize = false, Location = new Point(20, 62),
                Size = new Size(198, 26), BackColor = Color.Transparent
            };
            var l2 = new Label
            {
                Text = desc, Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoMudo,
                AutoSize = false, Location = new Point(20, 94), Size = new Size(198, 56),
                BackColor = Color.Transparent
            };
            p.Controls.AddRange(new Control[] { ic, l1, l2 });
        }

        // =================================================================
        //  PAGINA PERFIS
        // =================================================================
        private Panel MontarPaginaPerfis()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

            cartoesPerfil = new Cartao[3];
            string[] nomes = { "NUVEM", "LEVE", "PERSONALIZADO" };
            string[] descs = {
                "Desliga tudo. So o jogo, os launchers e o sistema minimo.\nO mais rapido e o mais leve.",
                "Mantem Discord, WhatsApp e o navegador.\nNao mexe no Update nem no mouse.",
                "Escolhe categoria por categoria."
            };
            Color[] cores = { Paleta.Roxo, Paleta.Azul, Paleta.Verde };
            string[] ids = { "nuvem", "leve", "personalizado" };

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var c = new Cartao { Clicavel = true, Location = new Point(34 + i * 340, 100), Size = new Size(320, 150) };
                c.CorBorda = Color.FromArgb(90, cores[i]);
                c.CorBordaHover = Color.FromArgb(170, cores[i]);
                c.Clicou += (s, e) => AplicarPerfil(ids[idx]);
                var tt = new Label
                {
                    Text = nomes[i], Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                    ForeColor = cores[i], AutoSize = false, Location = new Point(18, 20),
                    Size = new Size(284, 26), BackColor = Color.Transparent
                };
                var dd = new Label
                {
                    Text = descs[i], Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoMudo,
                    AutoSize = false, Location = new Point(18, 54), Size = new Size(284, 46),
                    BackColor = Color.Transparent
                };
                var st = new Label
                {
                    Text = "", Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Paleta.TextoFraco, AutoSize = false, Location = new Point(18, 112),
                    Size = new Size(284, 22), BackColor = Color.Transparent
                };
                c.Controls.AddRange(new Control[] { tt, dd, st });
                cartoesPerfil[i] = c;
                p.Controls.Add(c);
            }

            // --- categorias ---
            var cCat = new Cartao { Location = new Point(34, 266), Size = new Size(1002, 262) };
            cCat.Titulo("CATEGORIAS");
            cCat.Sub("marca o que o modo jogo deve desligar");
            caixas = new Caixa[CatIds.Length];
            for (int i = 0; i < CatIds.Length; i++)
            {
                int idx = i;
                var cx = new Caixa
                {
                    Texto = CatTexto[i], Detalhe = CatDetalhe[i], Marcada = true,
                    Location = new Point(20 + (i % 2) * 492, 68 + (i / 2) * 30),
                    Size = new Size(480, 28)
                };
                cx.Mudou += (s, e) => { catAtivas[idx] = cx.Marcada; AtualizarCartoesPerfil(); };
                caixas[i] = cx;
                cCat.Controls.Add(cx);
            }
            p.Controls.Add(cCat);

            // --- manter processos ---
            var cManter = new Cartao { Location = new Point(34, 544), Size = new Size(1002, 190) };
            cManter.Titulo("NAO FECHAR ESTES PROGRAMAS");
            cManter.Sub("separa por virgula - o resto e fechado");
            txtManter = new TextBox
            {
                Location = new Point(18, 60), Size = new Size(700, 24), Multiline = false,
                Text = manterProcessos, BackColor = Color.FromArgb(15, 13, 34),
                ForeColor = Paleta.Texto, Font = new Font("Consolas", 9F), BorderStyle = BorderStyle.FixedSingle
            };
            txtManter.TextChanged += (s, e) => manterProcessos = txtManter.Text;
            var lbManter = new Label
            {
                Text = "Easy Anti-Cheat, BattlEye, GameGuard, XignCode, nProtect e Valve Anti-Cheat estao SEMPRE protegidos.",
                Font = new Font("Segoe UI", 8F), ForeColor = Paleta.TextoFraco,
                AutoSize = false, Location = new Point(736, 62), Size = new Size(250, 20),
                BackColor = Color.Transparent
            };
            // Interruptor do VIGILANTE: o sistema que fecha sozinho, em segundo
            // plano, os programas que o Windows volta a abrir.
            caixaVigilante = new Caixa
            {
                Texto = "Vigilante: fechar automaticamente em segundo plano",
                Detalhe = "mantem o PC limpo enquanto o modo jogo estiver ligado",
                Marcada = true, Location = new Point(20, 96), Size = new Size(480, 28)
            };
            caixaVigilante.Mudou += (s, e) => { opcaoVigilante = caixaVigilante.Marcada; };
            caixaConfirmar = new Caixa
            {
                Texto = "Pedir confirmacao antes de fechar programas",
                Detalhe = "desliga para o modo jogo com um atalho, sem dialogo",
                Marcada = true, Location = new Point(20, 132), Size = new Size(480, 28)
            };
            caixaConfirmar.Mudou += (s, e) => { opcaoConfirmar = caixaConfirmar.Marcada; };
            cManter.Controls.AddRange(new Control[] { txtManter, lbManter, caixaVigilante, caixaConfirmar });
            p.Controls.Add(cManter);

            AtualizarCartoesPerfil();
            return p;
        }

        private void AplicarPerfil(string id)
        {
            perfil = id;
            if (id == "nuvem")
            {
                for (int i = 0; i < catAtivas.Length; i++) catAtivas[i] = true;
            }
            else if (id == "leve")
            {
                for (int i = 0; i < catAtivas.Length; i++) catAtivas[i] = true;
                catAtivas[Array.IndexOf(CatIds, "update")] = false;
                catAtivas[Array.IndexOf(CatIds, "desempenho")] = false;
            }
            else
            {
                // personalizado: o utilizador escolhe, mantemos o que estiver marcado
            }
            for (int i = 0; i < caixas.Length; i++) { caixas[i].Marcada = catAtivas[i]; caixas[i].Invalidate(); }
            AtualizarCartoesPerfil();
        }

        private void AtualizarCartoesPerfil()
        {
            if (cartoesPerfil == null) return;
            for (int i = 0; i < 3; i++)
            {
                cartoesPerfil[i].Selecionado = false;
                var st = cartoesPerfil[i].Controls.Count > 2 ? cartoesPerfil[i].Controls[2] as Label : null;
                if (st != null)
                {
                    int n = 0;
                    for (int j = 0; j < catAtivas.Length; j++) if (catAtivas[j]) n++;
                    string[] ids = { "nuvem", "leve", "personalizado" };
                    string[] hints = { "Desliga tudo - o mais leve", "Mantem o chat e o navegador", "Escolhe o que desligar" };
                    bool ativo = perfil == ids[i];
                    st.Text = ativo ? "PERFIL ATUAL - " + n + " categorias ligadas" : hints[i];
                    st.ForeColor = ativo ? Paleta.Verde : Paleta.TextoFraco;
                }
            }
            cartoesPerfil[0].Selecionado = perfil == "nuvem";
            cartoesPerfil[1].Selecionado = perfil == "leve";
            cartoesPerfil[2].Selecionado = perfil == "personalizado";
            cartoesPerfil[0].Invalidate(); cartoesPerfil[1].Invalidate(); cartoesPerfil[2].Invalidate();
        }

        // =================================================================
        //  PAGINA ESTADO (DIAGNOSTICO)
        // =================================================================
        private Panel MontarPaginaDiagnostico()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

            var c1 = new Cartao { Location = new Point(34, 100), Size = new Size(660, 460) };
            c1.Titulo("O QUE O MODO JOGO DESLIGOU");
            c1.Sub("lido do snapshot gravado antes de cada mudanca");
            txtDiag = new TextBox
            {
                Location = new Point(18, 66), Size = new Size(624, 374), Multiline = true,
                ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 13, 34),
                ForeColor = Paleta.TextoMudo, Font = new Font("Consolas", 8.5F), BorderStyle = BorderStyle.None
            };
            c1.Controls.Add(txtDiag);
            p.Controls.Add(c1);

            var c2 = new Cartao { Location = new Point(714, 100), Size = new Size(322, 460) };
            c2.Titulo("ESTADO AGORA");
            c2.Sub("o que o sistema esta a fazer neste instante");
            var info = new TextBox
            {
                Location = new Point(18, 66), Size = new Size(286, 240), Multiline = true,
                ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 13, 34),
                ForeColor = Paleta.TextoMudo, Font = new Font("Consolas", 8.5F), BorderStyle = BorderStyle.None
            };
            info.Name = "txtEstado";
            c2.Controls.Add(info);
            btnReparar = new Botao { Texto = "REPARAR (restaurar tudo)", Location = new Point(18, 320), Size = new Size(286, 36) };
            btnReparar.Clicou += (s, e) => Executar("Desativar");
            var lbRep = new Label
            {
                Text = "Restaura tudo a partir do snapshot,\nmesmo que algo tenha ficado para tras.",
                Font = new Font("Segoe UI", 8F), ForeColor = Paleta.TextoFraco, AutoSize = false,
                Location = new Point(18, 366), Size = new Size(286, 40), BackColor = Color.Transparent
            };
            var btnAtual = new Botao { Texto = "Atualizar leitura", Location = new Point(18, 410), Size = new Size(286, 32), CorFundo = Paleta.CartaoHover, CorHover = Paleta.Cartao };
            btnAtual.Clicou += (s, e) => CarregarDiagnostico();
            c2.Controls.AddRange(new Control[] { btnReparar, lbRep, btnAtual });
            p.Controls.Add(c2);

            return p;
        }

        private void CarregarDiagnostico()
        {
            if (txtDiag == null) return;

            // --- estado instantaneo ---
            var agora = new StringBuilder();
            agora.AppendLine("Processos ............ " + Process.GetProcesses().Length);
            agora.AppendLine("RAM livre ............ " + Math.Round(info.AvailablePhysicalMemory / 1073741824.0, 2) + " GB");
            agora.AppendLine("Modo jogo ............ " + (File.Exists(arquivoEstado) ? "ATIVO" : "desligado"));
            bool barra = BarraOculta();
            agora.AppendLine("Barra de tarefas ..... " + (barra ? "escondida" : "visivel"));
            agora.AppendLine("Plano de energia ..... " + PlanoAtual());
            try
            {
                var st = DefenderEstado();
                agora.AppendLine("Defender ............. " + st);
            }
            catch { }
            var txtAgora = Controls.Find("txtEstado", true);
            if (txtAgora.Length > 0)
            {
                var tb = txtAgora[0] as TextBox;
                if (tb != null) tb.Text = agora.ToString();
            }

            // --- snapshot ---
            var sb = new StringBuilder();
            if (!File.Exists(arquivoSnapshot))
            {
                sb.AppendLine("Ainda nao ha snapshot. Ative o modo jogo uma vez para o sistema");
                sb.AppendLine("aprender o estado do teu PC e saber desfazer tudo.");
                txtDiag.Text = sb.ToString();
                return;
            }
            try
            {
                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                js.MaxJsonLength = int.MaxValue;
                var o = js.DeserializeObject(File.ReadAllText(arquivoSnapshot));
                var dic = o as Dictionary<string, object>;
                if (dic == null) { txtDiag.Text = "Snapshot ilegivel."; return; }

                sb.AppendLine("DATA ......... " + Valor(dic, "Data"));
                sb.AppendLine("PERFIL ........ " + Valor(dic, "Perfil"));
                sb.AppendLine("CATEGORIAS .... " + Valor(dic, "Categorias"));
                sb.AppendLine("RESTAURADO .... " + Valor(dic, "Restaurado"));
                sb.AppendLine();

                var svcs = Lista(dic, "Servicos");
                sb.AppendLine("SERVICOS ALTERADOS (" + svcs.Count + ")");
                foreach (var s in svcs)
                {
                    var d = s as Dictionary<string, object>;
                    if (d == null) continue;
                    sb.AppendLine("   " + Texto(d, "Nome").PadRight(38) +
                                  Texto(d, "StartMode").PadRight(10) +
                                  (Texto(d, "Running") == "True" ? "rodava" : "parado"));
                }
                sb.AppendLine();

                var tar = Lista(dic, "Tarefas");
                sb.AppendLine("TAREFAS AGENDADAS DESATIVADAS (" + tar.Count + ")");
                foreach (var t in tar)
                {
                    var d = t as Dictionary<string, object>;
                    if (d == null) continue;
                    sb.AppendLine("   " + Texto(d, "Caminho") + Texto(d, "Nome"));
                }
                sb.AppendLine();

                var regs = Lista(dic, "Registro");
                sb.AppendLine("CHAVES DE REGISTO (" + regs.Count + ")");
                foreach (var r in regs)
                {
                    var d = r as Dictionary<string, object>;
                    if (d == null) continue;
                    sb.AppendLine("   " + Texto(d, "Caminho").Replace("HKCU:\\", "~").Replace("HKLM:\\", "#") + "  " +
                                  Texto(d, "Nome") + "  (antes: " + Texto(d, "Valor") + ")");
                }
                sb.AppendLine();

                var ifeo = Lista(dic, "Ifeo");
                sb.AppendLine("PROGRAMAS BLOQUEADOS (" + ifeo.Count + ")");
                sb.AppendLine("   " + string.Join(", ", ifeo.ConvertAll(x => x.ToString()).ToArray()));
                sb.AppendLine();

                var desk = Lista(dic, "AreaTrabalho");
                sb.AppendLine("ATALHOS ESCONDIDOS DA AREA DE TRABALHO (" + desk.Count + ")");
                foreach (var x in desk)
                {
                    var d = x as Dictionary<string, object>;
                    if (d == null) continue;
                    sb.AppendLine("   " + Texto(d, "Nome"));
                }
                var inic = Lista(dic, "Inicializacao");
                sb.AppendLine();
                sb.AppendLine("INICIALIZACAO ADIADA (" + inic.Count + ")");
                foreach (var x in inic)
                {
                    var d = x as Dictionary<string, object>;
                    if (d == null) continue;
                    sb.AppendLine("   " + Texto(d, "Nome"));
                }
                sb.AppendLine();
                sb.AppendLine("PLANO DE ENERGIA ORIGINAL ... " + Valor(dic, "PlanoEnergia"));
                sb.AppendLine("WALLPAPER ORIGINAL .......... " + Valor(dic, "WallpaperOriginal"));
            }
            catch (Exception ex)
            {
                sb.AppendLine("Erro a ler o snapshot: " + ex.Message);
            }
            txtDiag.Text = sb.ToString();
        }

        private static string Texto(Dictionary<string, object> d, string chave)
        {
            object v;
            if (d.TryGetValue(chave, out v) && v != null) return v.ToString();
            return "";
        }
        private static string Valor(Dictionary<string, object> d, string chave)
        {
            object v;
            if (d.TryGetValue(chave, out v) && v != null) return v.ToString();
            return "-";
        }
        private static List<object> Lista(Dictionary<string, object> d, string chave)
        {
            object v;
            if (d.TryGetValue(chave, out v) && v is List<object>) return (List<object>)v;
            return new List<object>();
        }
        private bool BarraOculta()
        {
            try
            {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                if (h == IntPtr.Zero) return false;
                return !IsWindowVisible(h);
            }
            catch { return false; }
        }

        private string PlanoAtual()
        {
            try
            {
                IntPtr guid;
                uint r = PowerGetActiveScheme(IntPtr.Zero, out guid);
                if (r != 0) return "?";
                var b = new byte[16];
                Marshal.Copy(guid, b, 0, 16);
                Marshal.FreeHGlobal(guid);
                // o GUID vem em memoria little-endian nos primeiros 3 blocos
                string s = b[3].ToString("X2") + b[2].ToString("X2") + b[1].ToString("X2") + b[0].ToString("X2") + "-" +
                           b[5].ToString("X2") + b[4].ToString("X2") + "-" +
                           b[7].ToString("X2") + b[6].ToString("X2") + "-" +
                           b[8].ToString("X2") + b[9].ToString("X2") + "-" +
                           b[10].ToString("X2") + b[11].ToString("X2") + b[12].ToString("X2") +
                           b[13].ToString("X2") + b[14].ToString("X2") + b[15].ToString("X2");
                return s;
            }
            catch { return "?"; }
        }

        private string DefenderEstado()
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -Command \"$s=Get-MpComputerStatus; if($s.RealTimeProtectionEnabled){'LIGADO'}else{'DESLIGADO'}\"");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                using (var pr = Process.Start(psi))
                {
                    string r = pr.StandardOutput.ReadToEnd().Trim();
                    if (r == "LIGADO") return "tempo real LIGADO";
                    if (r == "DESLIGADO") return "tempo real DESLIGADO";
                    return "estado desconhecido";
                }
            }
            catch { return "nao foi possivel ler"; }
        }
        // =================================================================
        //  PAGINA AJUSTES
        // =================================================================
        private Panel MontarPaginaConfig()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

            var c1 = new Cartao { Location = new Point(34, 100), Size = new Size(492, 200) };
            c1.Titulo("PASTA DO APLICATIVO");
            c1.Sub("escolha onde os arquivos ficam");
            var lb1 = new Label
            {
                Text = DirBase, Font = new Font("Consolas", 9F), ForeColor = Paleta.RoxoClaro,
                AutoSize = false, Location = new Point(18, 72), Size = new Size(456, 22),
                BackColor = Color.Transparent
            };
            var lb2 = new Label
            {
                Text = "O aplicativo, o script e os wallpapers ficam aqui.\nSe voce mover, escolha a nova pasta e clique em Aplicar.",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoFraco, AutoSize = false,
                Location = new Point(18, 102), Size = new Size(456, 46), BackColor = Color.Transparent
            };
            var b1 = new Botao { Texto = "Escolher pasta...", Location = new Point(18, 152), Size = new Size(170, 32) };
            b1.Clicou += (s, e) => EscolherPasta();
            c1.Controls.AddRange(new Control[] { lb1, lb2, b1 });
            p.Controls.Add(c1);

            var c2 = new Cartao { Location = new Point(542, 100), Size = new Size(494, 200) };
            c2.Titulo("WALLPAPER DO MODO JOGO");
            c2.Sub("o que aparece quando voce ativa");
            var pv1 = new PictureBox { Image = Preview(Path.Combine(DirBase, "wallpapers", "jogo.jpg")), Location = new Point(18, 72), Size = new Size(140, 84), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(15, 13, 34) };
            var pv2 = new PictureBox { Image = Preview(Path.Combine(DirBase, "wallpapers", "normal.jpg")), Location = new Point(168, 72), Size = new Size(140, 84), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(15, 13, 34) };
            pv1.Click += (s, e) => { wallSelecionado = Path.Combine(DirBase, "wallpapers", "jogo.jpg"); SalvarWallpadrao(); };
            pv2.Click += (s, e) => { wallSelecionado = Path.Combine(DirBase, "wallpapers", "normal.jpg"); SalvarWallpadrao(); };
            pv1.Cursor = pv2.Cursor = Cursors.Hand;
            var lbw = new Label
            {
                Text = "Clique numa imagem para definir como wallpaper do jogo.",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoFraco, AutoSize = false,
                Location = new Point(18, 162), Size = new Size(456, 20), BackColor = Color.Transparent
            };
            c2.Controls.AddRange(new Control[] { pv1, pv2, lbw });
            p.Controls.Add(c2);

            // --- PESQUISA DO WINDOWS ---
            var c3 = new Cartao { Location = new Point(34, 316), Size = new Size(492, 200) };
            c3.Titulo("PESQUISA DO WINDOWS");
            c3.Sub("desativa sempre, fora e dentro do modo jogo");
            var lb3 = new Label
            {
                Text = "Tira a caixa \"Pesquisar\" da barra de tarefas, bloqueia o\n" +
                       "SearchHost e o indexador. Poupa cerca de 75 a 160 MB.\n\n" +
                       "Esta opcao e independente: o MODO NORMAL nao a desfaz.\n" +
                       "No Windows 11 a caixa da barra de tarefas so some pelas Definicoes.",
                Location = new Point(18, 68), Size = new Size(456, 62), BackColor = Color.Transparent
            };
            btnBusca = new Botao { Texto = "", Location = new Point(18, 140), Size = new Size(250, 34) };
            btnBusca.Clicou += (s, e) => ExecutarBusca();
            lblBusca = new Label
            {
                Text = "", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Paleta.TextoFraco,
                AutoSize = false, Location = new Point(280, 148), Size = new Size(196, 22), BackColor = Color.Transparent
            };
            btnBuscaDefs = new Botao { Texto = "Definicoes da barra de tarefas...", Location = new Point(18, 160), Size = new Size(250, 30), CorFundo = Paleta.CartaoHover, CorHover = Paleta.Cartao };
            btnBuscaDefs.Clicou += (s, e) => AbrirDefinicoesTaskbar();
            c3.Controls.AddRange(new Control[] { lb3, btnBusca, lblBusca, btnBuscaDefs });
            p.Controls.Add(c3);

            // --- MANUTENCAO ---
            var c4 = new Cartao { Location = new Point(542, 316), Size = new Size(494, 200) };
            c4.Titulo("MANUTENCAO");
            c4.Sub("opcoes que alteram o sistema");
            var lb4 = new Label
            {
                Text = "Desinstalar o pacote do menu Iniciar\nSome o StartMenuExperienceHost (cerca de 130 MB). Custo: o menu Iniciar para de funcionar ate o Windows recadastrar o pacote.",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoMudo, AutoSize = false,
                Location = new Point(18, 68), Size = new Size(300, 44), BackColor = Color.Transparent
            };
            var b4 = new Botao { Texto = "Remover / reinstalar", Location = new Point(326, 70), Size = new Size(150, 32), CorFundo = Paleta.CartaoHover, CorHover = Paleta.Cartao };
            b4.Clicou += (s, e) => AlternarAppX();
            var lb5 = new Label
            {
                Text = "Restaurar tudo agora\nDesfaz bloqueios, religa servicos, tarefas, registo, energia, area de trabalho e wallpaper. O botao MODO NORMAL faz o mesmo.",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoMudo, AutoSize = false,
                Location = new Point(18, 126), Size = new Size(300, 40), BackColor = Color.Transparent
            };
            var b5 = new Botao { Texto = "Restaurar agora", Location = new Point(326, 128), Size = new Size(150, 32) };
            b5.Clicou += (s, e) => Executar("Desativar");
            c4.Controls.AddRange(new Control[] { lb4, b4, lb5, b5 });
            p.Controls.Add(c4);

            var c5 = new Cartao { Location = new Point(34, 532), Size = new Size(1002, 108) };
            c5.Titulo("SOBRE");
            c5.Sub("codigo aberto, sem ficheiros ocultos");
            var lb6 = new Label
            {
                Text = "O aplicativo e um EXE nativo do Windows em C# (.NET Framework) que chama um script em PowerShell. " +
                       "Tudo o que e alterado fica gravado num snapshot antes de mexer, para poder ser desfeito ao milimetro.\n\n" +
                       "  " + DirBase + "\\n" +
                       "    ModoJogo.exe     aplicativo e interface       ModoJogo.ps1   o motor (snapshot, perfis, restauro)\n" +
                       "    app\\            os codigos .cs               build.cmd      recompila tudo com um comando",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Paleta.TextoMudo, AutoSize = false,
                Location = new Point(18, 64), Size = new Size(966, 40), BackColor = Color.Transparent
            };
            c5.Controls.Add(lb6);
            p.Controls.Add(c5);

            AtualizarBotaoBusca();
            return p;
        }

        // O botao da pesquisa mostra sempre o estado real (le o busca.json).
        private void AtualizarBotaoBusca()
        {
            if (btnBusca == null) return;
            bool off = File.Exists(Path.Combine(EstadoDir, "busca.json"));
            btnBusca.Texto = off ? "REPOR A PESQUISA" : "DESATIVAR A PESQUISA";
            btnBusca.CorFundo = off ? Paleta.CartaoHover : Paleta.Roxo;
            btnBusca.CorHover = off ? Paleta.Cartao : Paleta.RoxoClaro;
            if (lblBusca != null)
            {
                lblBusca.Text = off ? "DESATIVADA" : "ATIVA (gasta ~75 MB)";
                lblBusca.ForeColor = off ? Paleta.Verde : Paleta.Laranja;
            }
        }

        // O Windows 11 24H2 guarda a caixa "Pesquisar" da barra de tarefas num
        // sitio que o registo nao controla. A unica forma garantida e pela
        // propria pagina de Definicoes - por isso abrimos essa pagina.
        private void AbrirDefinicoesTaskbar()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true });
                Anexar("Aberto: Definicoes > Barra de tarefas > Itens da barra de tarefas > Pesquisar > Desligar");
            }
            catch (Exception ex) { Anexar("Nao consegui abrir as Definicoes: " + ex.Message); }
        }

        private void ExecutarBusca()
        {
            if (rodando) return;
            if (!File.Exists(scriptCaminho)) { Anexar("Script nao encontrado: " + scriptCaminho); return; }
            bool off = File.Exists(Path.Combine(EstadoDir, "busca.json"));
            string estado = off ? "off" : "on";

            acaoAtual = "Busca";
            rodando = true;
            txtLog.Clear();
            linhasLidas = 0;
            inicioExec = DateTime.Now;
            lblStatus.Text = off ? "A repor a pesquisa do Windows..." : "A desativar a pesquisa do Windows...";
            lblStatus.ForeColor = Paleta.Laranja;
            AtualizarBotoes(File.Exists(arquivoEstado));

            try { if (File.Exists(arquivoSaida)) File.Delete(arquivoSaida); } catch { }
            string cmdLine = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" +
                             scriptCaminho + "\" -Acao Busca -EstadoBusca " + estado +
                             " -BaseDir \"" + DirBase + "\"";
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmdLine + " > \"" + arquivoSaida + "\" 2>&1")
                {
                    UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
                };
                procExec = new Process { StartInfo = psi };
                procExec.Start();
                timerExec.Start();
            }
            catch (Exception ex)
            {
                rodando = false;
                lblStatus.Text = "Falhou: " + ex.Message;
                lblStatus.ForeColor = Paleta.Vermelho;
                Anexar("ERRO: " + ex.Message);
                AtualizarEstado();
            }
        }

        // Carrega a imagem SEM deixar o ficheiro bloqueado.
        // Image.FromFile mantem o handle aberto e impedia de substituir o wallpaper.
        private Image Preview(string caminho)
        {
            try
            {
                if (!File.Exists(caminho)) return null;
                using (var ms = new MemoryStream(File.ReadAllBytes(caminho)))
                using (var img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        // =================================================================
        //  PASTA
        // =================================================================
        private void EscolherPasta()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Escolha a pasta onde o Modo Jogo vai ficar";
                dlg.SelectedPath = DirBase;
                if (dlg.ShowDialog() == DialogResult.OK) NovaPasta(dlg.SelectedPath);
            }
        }

        private void NovaPasta(string destino)
        {
            try
            {
                string origem = DirBase;
                if (destino.TrimEnd('\\').Equals(origem.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;

                Directory.CreateDirectory(destino);
                foreach (var item in new[] { "ModoJogo.exe", "ModoJogo.ps1", "Limpeza.ps1" })
                {
                    string s = Path.Combine(origem, item);
                    if (File.Exists(s)) File.Copy(s, Path.Combine(destino, item), true);
                }
                string w = Path.Combine(origem, "wallpapers");
                if (Directory.Exists(w)) CopyDir(w, Path.Combine(destino, "wallpapers"));
                // A pasta app\ tem o codigo-fonte que a pagina SOBRE apresenta.
                // Sem copiar, a pagina passava a mentir depois da mudanca.
                string a = Path.Combine(origem, "app");
                if (Directory.Exists(a)) CopyDir(a, Path.Combine(destino, "app"));

                DirBase = destino;
                scriptCaminho = Path.Combine(DirBase, "ModoJogo.ps1");
                pastaWallpapers = Path.Combine(DirBase, "wallpapers");
                if (!File.Exists(wallSelecionado))
                    wallSelecionado = Path.Combine(pastaWallpapers, "jogo.jpg");

                Directory.CreateDirectory(EstadoDir);
                File.WriteAllText(Path.Combine(EstadoDir, "pasta.txt"), DirBase);

                Anexar("Pasta alterada para: " + DirBase);
                MessageBox.Show("Pronto! O aplicativo agora usa:\n\n" + DirBase,
                                "Pasta alterada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nao deu para mudar a pasta:\n\n" + ex.Message,
                                "Erro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void CopyDir(string de, string para)
        {
            Directory.CreateDirectory(para);
            foreach (var f in Directory.GetFiles(de)) File.Copy(f, Path.Combine(para, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(de)) CopyDir(d, Path.Combine(para, Path.GetFileName(d)));
        }

        private void AlternarAppX()
        {
            string nome = "MicrosoftWindows.StartMenuExperienceHost";
            bool instalado = PacoteInstalado(nome);
            var r = MessageBox.Show(
                instalado
                ? "Isso DESINSTALA o pacote do menu Iniciar e libera cerca de 130 MB.\n\n" +
                  "O menu Iniciar para de funcionar. O Windows pode recadastrar o pacote numa atualizacao.\n\nContinuar?"
                : "Isso REINSTALA o pacote do menu Iniciar.\n\nVai abrir a Microsoft Store.\n\nContinuar?",
                instalado ? "Remover pacote" : "Reinstalar pacote",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            if (instalado)
            {
                try
                {
                    var psi = new ProcessStartInfo("powershell.exe",
                        "-NoProfile -Command \"Get-AppxPackage -Name '" + nome +
                        "' | Remove-AppxPackage\"")
                    { UseShellExecute = true, Verb = "runas" };
                    Process.Start(psi);
                    Anexar("Pacote do menu Iniciar enviado para remocao. Reinicie para concluir.");
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Erro"); }
            }
            else
            {
                try { Process.Start("ms-windows-store://pdp/?PFN=" + nome + "_cw5n1h2txyewy!App"); }
                catch { Process.Start("https://apps.microsoft.com"); }
            }
        }

        private static bool PacoteInstalado(string nome)
        {
            try
            {
                string raiz = @"C:\Program Files\WindowsApps";
                if (!Directory.Exists(raiz)) return false;
                return Directory.GetDirectories(raiz, "MicrosoftWindows." + nome + "*").Length > 0;
            }
            catch { return false; }
        }

        // =================================================================
        //  MEDICAO
        // =================================================================
        private void Medir()
        {
            try
            {
                double total = info.TotalPhysicalMemory / 1073741824.0;
                double livre = info.AvailablePhysicalMemory / 1073741824.0;
                int pctRam = (int)Math.Round((1 - livre / total) * 100);
                gMedidorRam.Atualizar(pctRam);
                gMedidorRam.Cor = pctRam >= 85 ? Paleta.Vermelho : pctRam >= 65 ? Paleta.Laranja : Paleta.Roxo;

                try
                {
                    if (pcCpu == null) pcCpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    // A primeira chamada a NextValue devolve sempre 0 (sem amostra
                    // anterior). A amostra valida e a segunda chamada.
                    pcCpu.NextValue();
                    float bruto = pcCpu.NextValue();
                    if (bruto > 0)
                    {
                        int pctCpu = (int)Math.Round(bruto);
                        pctCpu = Math.Max(0, pctCpu - 3);
                        gMedidorCpu.Atualizar(pctCpu);
                        gMedidorCpu.Cor = pctCpu >= 80 ? Paleta.Vermelho : pctCpu >= 50 ? Paleta.Laranja : Paleta.Cyan;
                    }
                }
                catch { }

                processosAgora = Process.GetProcesses().Length;
                if (lblProcessos != null) lblProcessos.Text = processosAgora.ToString();
                if (lblProcessosDelta != null)
                {
                    if (processosAntes > 0)
                    {
                        int d = processosAntes - processosAgora;
                        lblProcessosDelta.Text = (d > 0 ? "-" + d : "+" + (-d)) + " de " + processosAntes;
                        lblProcessosDelta.ForeColor = d > 0 ? Paleta.Verde : Paleta.Laranja;
                    }
                    else lblProcessosDelta.Text = "";
                }
            }
            catch { }
        }

        // =================================================================
        //  ESTADO E BOTOES
        // =================================================================
        private void AtualizarEstado()
        {
            bool ativo = File.Exists(arquivoEstado);
            tagModo.Texto = ativo ? "MODO JOGO ATIVO" : "MODO NORMAL";
            tagModo.Cor = ativo ? Paleta.Roxo : Paleta.Azul;
            tagModo.Invalidate();
            AtualizarBotoes(ativo);
        }

        // O botao MODO NORMAL era desactivado em Executar() e NUNCA voltava a
        // ficar activo: depois da primeira accao ficava morto para sempre.
        private void AtualizarBotoes(bool ativo)
        {
            cartaoJogo.Clicavel = !ativo && !rodando;
            cartaoJogo.Desabilitado = ativo || rodando;
            cartaoNormal.Clicavel = !rodando;
            cartaoNormal.Desabilitado = rodando;
            cartaoJogo.Invalidate();
            cartaoNormal.Invalidate();
        }

        // =================================================================
        //  EXECUCAO
        // =================================================================
        // O MODO JOGO ja nao abre o seletor de wallpaper: a escolha dava
        // demasiados bugs (a janela do jogo abria por tras do dialogo).
        // O wallpaper continua a poder-se escolher na pagina AJUSTES.
        private void AcionarComWallpaperSalvo()
        {
            Executar("Ativar");
        }

        private void SalvarWallpadrao()
        {
            try
            {
                Directory.CreateDirectory(EstadoDir);
                File.WriteAllText(arquivoWallpadrao, wallSelecionado);
            }
            catch { }
        }

        private void Executar(string acao)
        {
            if (rodando) return;
            if (!File.Exists(scriptCaminho))
            {
                Anexar("Script nao encontrado em: " + scriptCaminho);
                lblStatus.Text = "Script nao encontrado."; lblStatus.ForeColor = Paleta.Vermelho;
                return;
            }
            acaoAtual = acao;

            if (acao == "Ativar") ConfirmarAtivacao();

            rodando = true;
            txtLog.Clear();
            linhasLidas = 0;
            inicioExec = DateTime.Now;
            processosAntes = Process.GetProcesses().Length;
            ramAntes = Math.Round(info.AvailablePhysicalMemory / 1073741824.0, 2);
            lblStatus.Text = acao == "Ativar" ? "Ativando o modo jogo..." : "Restaurando o modo normal...";
            lblStatus.ForeColor = Paleta.Laranja;
            AtualizarBotoes(File.Exists(arquivoEstado));

            try { if (File.Exists(arquivoSaida)) File.Delete(arquivoSaida); } catch { }
            try { if (File.Exists(arquivoResultado)) File.Delete(arquivoResultado); } catch { }

            string cmdLine = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" +
                             scriptCaminho + "\" -Acao " + acao +
                             " -BaseDir \"" + DirBase + "\"" +
                             " -Perfil " + perfil;
            if (perfil == "personalizado")
            {
                string cs = "";
                for (int i = 0; i < catAtivas.Length; i++)
                    if (catAtivas[i]) cs += (cs == "" ? "" : ",") + CatIds[i];
                if (cs != "") cmdLine += " -Categorias \"" + cs + "\"";
            }
            if (!string.IsNullOrEmpty(manterProcessos))
                cmdLine += " -Manter \"" + manterProcessos.Replace("\"", "") + "\"";
            if (acao == "Ativar" && !string.IsNullOrEmpty(wallSelecionado))
                cmdLine += " -Wallpaper \"" + wallSelecionado + "\"";

            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmdLine + " > \"" + arquivoSaida + "\" 2>&1")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                procExec = new Process { StartInfo = psi };
                procExec.Start();
                timerExec.Start();
            }
            catch (Exception ex)
            {
                rodando = false;
                lblStatus.Text = "Falhou: " + ex.Message;
                lblStatus.ForeColor = Paleta.Vermelho;
                Anexar("ERRO ao iniciar: " + ex.Message);
                AtualizarEstado();
            }
        }

        private void ConfirmarAtivacao()
        {
            // O dialogo tem de ser visivel: se a app estiver minimizada,
            // a mensagem aparece escondida e o modo jogo nunca arranca.
            if (!opcaoConfirmar) { return; }
            RestaurarApp();
            var abertos = new System.Collections.Generic.List<string>();
            foreach (var p in Process.GetProcesses())
            {
                try
            {
                string n = p.ProcessName;
                if (n == "OpenCode" || n == "opencode-cli" || n == "Code" ||
                    n == "RobloxStudio" || n == "devenv" || n == "obs64" || n == "explorer")
                    continue;
                if (manterProcessos != null && manterProcessos.ToLower().Contains(n.ToLower())) continue;
                if (n == "msedge" || n == "msedgewebview2" || n == "mscopilot" || n == "OneDrive" ||
                    n == "SearchHost" || n == "Widgets" || n == "Spotify" || n == "Discord" ||
                    n == "chrome" || n == "WhatsApp" || n == "steam" || n == "EpicGamesLauncher" ||
                    n == "RobloxPlayerBeta" || n == "Fortnite" || n == "Nebula") continue;
                if (n.Length < 3) continue;
                if (!abertos.Contains(n)) abertos.Add(n);
            }
            catch { }
            }
            if (abertos.Count == 0) return;
            string lista = string.Join(", ", abertos.ToArray());
            if (lista.Length > 220) lista = lista.Substring(0, 220) + "...";
            var r = MessageBox.Show(
                "O modo jogo vai FECHAR estes programas:\n\n" + lista +
                "\n\nOs que precisas de manter ficam na pagina PERFIS.\n\nContinuar?",
                "Confirmar modo jogo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Le o log enquanto o script corre: o registo aparece linha a linha
        // em vez de tudo de uma vez no fim.
        // Processos que o VIGILANTE fecha de 2 em 2 segundos enquanto o modo
        // jogo esta ligado. O Windows relanca varios deles sozinho (pesquisa,
        // widgets, noticias do Inicio, assistants), e sem o vigilante voltam
        // a aparecer e a comer RAM.
        //
        // NUNCA entram aqui: o jogo, os launchers, o anti-cheat, o WebView2
        // (msedgewebview2 = motor de interface da Epic e do Roblox) e o
        // driver de video. Matar o WebView2 deixa a janela PRETA.
        private static readonly string[] ProcessosRuidosos = {
            // pesquisa do Windows
            "SearchHost", "SearchApp", "SearchIndexer", "SearchProtocolHost", "SearchFilterHost",
            // widgets, painel de noticias e Inicio
            "Widgets", "WidgetService", "WidgetBoard", "MicrosoftStartFeedProvider",
            "StartMenuExperienceHost", "SearchApp",
            // browsers e assistants que se autolancam
            "msedge", "mscopilot", "Copilot", "MicrosoftTeams", "ms-teams", "Teams",
            "OneDrive", "OneDrive.Sync.Service", "Dropbox",
            // processos de servico que o shell volta a levantar
            "UserOOBEBroker", "AQAUserPS", "ACCUserPS", "WUDFHost", "WpcMon",
            "AcerQAAgent", "AcerCCAgent", "AcerDIAgent", "AcerService",
            "AcerServiceWrapper", "AcerSysHardwareService", "AcerGAICameraService",
            "IntelGraphicsSoftware.Overlay", "PresentMonService", "PresentMonSharedService"
        };

        private static readonly string[] ProcessosPesquisa = {
            "SearchHost", "SearchApp", "SearchIndexer", "SearchProtocolHost", "SearchFilterHost"
        };

        // Mata a pesquisa quando o modo jogo esta ligado ou quando a pesquisa
        // foi desactivada de proposito. Fora dessas condicoes nao se mexe.
        // O VIGILANTE: fecha os processos que o Windows relanca sozinho e
        // volta a esconder a barra de tarefas. Corre enquanto o modo jogo esta
        // ligado, mesmo com a app minimizada.
        private void VigiarPesquisa()
        {
            try
            {
                bool modoJogo = File.Exists(arquivoEstado);
                bool pesquisaOff = File.Exists(Path.Combine(EstadoDir, "busca.json"));
                if (!modoJogo && !pesquisaOff) return;

                // A pesquisa e sempre fora, mesmo fora do modo jogo.
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (Array.IndexOf(ProcessosPesquisa, p.ProcessName) >= 0) p.Kill();
                    }
                    catch { }
                }

                if (!modoJogo || !opcaoVigilante) return;

                // O resto dos ruido so no modo jogo.
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (Array.IndexOf(ProcessosRuidosos, p.ProcessName) >= 0) p.Kill();
                    }
                    catch { }
                }

                // O Windows volta a mostrar a barra de tarefas sozinho.
                if (opcaoVigiarBarra) EsconderBarra();
            }
            catch { }
        }

        // Minimiza a app para nao tapar o jogo ou o launcher.
        private void MinimizarParaSegundoPlano()
        {
            try
            {
                if (WindowState != FormWindowState.Minimized) WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }
            catch { }
        }

        // Traz a app de volta quando o utilizador precisa dela.
        private void RestaurarApp()
        {
            try
            {
                ShowInTaskbar = true;
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Show();
                Activate();
            }
            catch { }
        }

        // Esconde a barra de tarefas (a janela Shell_TrayWnd).
        private void EsconderBarra()
        {
            try
            {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                if (h != IntPtr.Zero) ShowWindow(h, 0);
            }
            catch { }
        }

        private void Acompanhar()
        {
            LerLinhasNovas();
            if (procExec == null) { timerExec.Stop(); return; }
            try
            {
                if (procExec.HasExited)
                {
                    timerExec.Stop();
                    Fim(acaoAtual);
                }
            }
            catch { }
        }

        private string acaoAtual = "";

        private void LerLinhasNovas()
        {
            try
            {
                if (!File.Exists(arquivoSaida)) return;
                using (var fs = new FileStream(arquivoSaida, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string todo = sr.ReadToEnd();
                    var linhas = todo.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (linhas.Length <= linhasLidas) return;
                    for (int i = linhasLidas; i < linhas.Length; i++) Anexar(linhas[i]);
                    linhasLidas = linhas.Length;
                }
            }
            catch { }
        }

        private void Fim(string acao)
        {
            LerLinhasNovas();
            rodando = false;

            bool ok = false;
            try { if (File.Exists(arquivoResultado)) ok = File.ReadAllText(arquivoResultado).Trim().StartsWith("OK"); }
            catch { }

            double dur = (DateTime.Now - inicioExec).TotalSeconds;
            if (acao == "Busca")
            {
                AtualizarBotaoBusca();
                if (ok)
                {
                    bool desativada = File.Exists(Path.Combine(EstadoDir, "busca.json"));
                    lblStatus.Text = desativada ? "Pesquisa do Windows desativada. Poupa cerca de 75 MB." : "Pesquisa do Windows reposta.";
                    lblStatus.ForeColor = desativada ? Paleta.Verde : Paleta.Azul;
                }
                else
                {
                    lblStatus.Text = "Nao consegui mudar a pesquisa. Veja o registo.";
                    lblStatus.ForeColor = Paleta.Vermelho;
                }
                Anexar("--- PESQUISA em " + Math.Round(dur) + "s ---");
                AtualizarEstado();
                return;
            }
            if (acao == "Ativar")
            {
                if (ok)
                {
                    try { File.WriteAllText(arquivoEstado, DateTime.Now.ToString()); } catch { }
                    int restantes = Process.GetProcesses().Length;
                    lblStatus.Text = "Modo jogo ativo em " + Math.Round(dur) + "s  |  " +
                                     processosAntes + " -> " + restantes + " processos  |  " +
                                     "RAM +" + Math.Round(info.AvailablePhysicalMemory / 1073741824.0 - ramAntes, 2) + " GB";
                    lblStatus.ForeColor = Paleta.Verde;
                    // A app e SEMPRE NO TOPO. Se ficar aberta tapa a janela do
                    // launcher e do jogo. Ao ativar, ela minimiza-se.
                    MinimizarParaSegundoPlano();
                }
                else
                {
                    lblStatus.Text = "O modo jogo falhou. Veja o registo abaixo.";
                    lblStatus.ForeColor = Paleta.Vermelho;
                }
            }
            else
            {
                if (ok)
                {
                    try { if (File.Exists(arquivoEstado)) File.Delete(arquivoEstado); } catch { }
                    lblStatus.Text = "Modo normal restaurado em " + Math.Round(dur) + "s. Tudo como estava.";
                    lblStatus.ForeColor = Paleta.Azul;
                }
                else
                {
                    lblStatus.Text = "A restauracao deu erros. Use REPARAR no separador ESTADO.";
                    lblStatus.ForeColor = Paleta.Laranja;
                }
            }
            Anexar("");
            Anexar("--- " + (acao == "Ativar" ? "MODO JOGO" : "MODO NORMAL") + " em " +
                   Math.Round(dur) + "s ---");
            AtualizarEstado();
        }

        private void Anexar(string linha)
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(() => Anexar(linha))); } catch { } return; }
            string l = linha.Trim();
            if (l.Length == 0) return;
            txtLog.AppendText(l + Environment.NewLine);
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }
    }
}















