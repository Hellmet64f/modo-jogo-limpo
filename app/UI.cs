using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ModoJogoApp
{
    // ============================================================
    //  Paleta  -  roxo/azul escuro no mesmo tom da referencia
    // ============================================================
    public static class Paleta
    {
        public static readonly Color FundoA = Color.FromArgb(11, 10, 24);
        public static readonly Color FundoB = Color.FromArgb(23, 18, 48);
        public static readonly Color Cartao = Color.FromArgb(30, 26, 56);
        public static readonly Color CartaoHover = Color.FromArgb(40, 34, 72);
        public static readonly Color Borda = Color.FromArgb(52, 45, 88);
        public static readonly Color BordaHover = Color.FromArgb(96, 78, 190);

        public static readonly Color Roxo = Color.FromArgb(139, 92, 246);
        public static readonly Color RoxoClaro = Color.FromArgb(167, 139, 250);
        public static readonly Color Azul = Color.FromArgb(59, 130, 246);
        public static readonly Color Cyan = Color.FromArgb(34, 211, 238);
        public static readonly Color Verde = Color.FromArgb(34, 197, 94);
        public static readonly Color Vermelho = Color.FromArgb(239, 68, 68);
        public static readonly Color Laranja = Color.FromArgb(251, 146, 60);

        public static readonly Color Texto = Color.FromArgb(237, 235, 245);
        public static readonly Color TextoMudo = Color.FromArgb(158, 150, 188);
        public static readonly Color TextoFraco = Color.FromArgb(110, 103, 140);
    }

    public static class Desenho
    {
        public static GraphicsPath Cantos(int w, int h, int r)
        {
            var p = new GraphicsPath();
            if (r <= 0) { p.AddRectangle(new Rectangle(0, 0, w, h)); return p; }
            int d = r * 2;
            p.AddArc(0, 0, d, d, 180, 90);
            p.AddArc(w - d, 0, d, d, 270, 90);
            p.AddArc(w - d, h - d, d, d, 0, 90);
            p.AddArc(0, h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Card(Graphics g, Rectangle r, int raio, Color fundo, Color borda, int larguraBorda = 1)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Cantos(r.Width, r.Height, raio))
            using (var b = new SolidBrush(fundo))
                g.FillPath(b, p);
            if (larguraBorda > 0)
                using (var p = Cantos(r.Width, r.Height, raio))
                using (var pen = new Pen(borda, larguraBorda))
                    g.DrawPath(pen, p);
        }

        public static void Texto(Graphics g, string t, Font f, Color c, Rectangle r, StringFormat fmt)
        {
            fmt.Alignment = StringAlignment.Near;
            fmt.LineAlignment = StringAlignment.Center;
            fmt.Trimming = StringTrimming.EllipsisCharacter;
            g.DrawString(t, f, new SolidBrush(c), r, fmt);
        }

        public static StringFormat Formato(StringAlignment al, StringAlignment lin)
        {
            return new StringFormat(StringFormatFlags.LineLimit)
            {
                Alignment = al,
                LineAlignment = lin,
                Trimming = StringTrimming.EllipsisCharacter
            };
        }
    }

    // ------------------------------------------------------------
    //  Painel com fundo em gradiente
    // ------------------------------------------------------------
    public class FundoGradiente : Panel
    {
        public Color A = Paleta.FundoA;
        public Color B = Paleta.FundoB;
        public bool Diagonal = true;

        public FundoGradiente()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width, Height);
            // O 3o parametro do LinearGradientBrush e LinearGradientMode (enum),
            // nao um float de angulo.
            var modo = Diagonal ? LinearGradientMode.ForwardDiagonal : LinearGradientMode.Vertical;
            using (var br = new LinearGradientBrush(r, A, B, modo))
                g.FillRectangle(br, r);
            // Brilhos suaves no canto superior direito
            using (var br = new GraphicsPath())
            {
                br.AddEllipse((int)(Width * 0.55f), (int)(-Height * 0.55f),
                              (int)(Width * 0.9f), (int)(Height * 0.95f));
                using (var gb = new PathGradientBrush(br))
                {
                    gb.CenterColor = Color.FromArgb(38, Paleta.Roxo);
                    gb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                    g.FillPath(gb, br);
                }
            }
            using (var br = new GraphicsPath())
            {
                br.AddEllipse((int)(-Width * 0.25f), (int)(Height * 0.62f),
                              (int)(Width * 0.7f), (int)(Height * 0.75f));
                using (var gb = new PathGradientBrush(br))
                {
                    gb.CenterColor = Color.FromArgb(30, Paleta.Azul);
                    gb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                    g.FillPath(gb, br);
                }
            }
        }
    }

    // ------------------------------------------------------------
    //  Cartao com cantos arredondados
    // ------------------------------------------------------------
    public class Cartao : Panel
    {
        public int Raio = 16;
        public Color CorFundo = Paleta.Cartao;
        // CorBorda e o ACENTO do cartao (roxo/azul). O hover trabalha numa cor
        // temporaria separada: antes o OnMouseLeave repunha a cor base e o
        // acento do cartao sumia sozinho assim que o rato saia de cima.
        private Color corBorda = Paleta.Borda;
        private Color corBordaTemporaria = Paleta.Borda;
        public Color CorBorda
        {
            get { return corBorda; }
            set { corBorda = value; corBordaTemporaria = value; Invalidate(); }
        }
        public Color CorBordaHover = Paleta.BordaHover;
        public bool Selecionado = false;
        public bool Desabilitado = false;

        private bool clicavel = false;
        public bool Clicavel
        {
            get { return clicavel; }
            set
            {
                clicavel = value;
                // O cursor e o foco acompanhavam o valor antigo: ficavam de "mao"
                // em cartoes que ja nao acceptavam clique.
                Cursor = value ? Cursors.Hand : Cursors.Default;
                TabStop = value;
                Invalidate();
            }
        }

        public event EventHandler Clicou;

        public Cartao()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Default;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Clicavel && !Desabilitado) { CorFundo = Paleta.CartaoHover; corBordaTemporaria = CorBordaHover; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (Clicavel && !Desabilitado) { CorFundo = Paleta.Cartao; corBordaTemporaria = corBorda; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Clicavel && !Desabilitado && Clicou != null) Clicou(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys k)
        {
            // Permite ativar o cartao com ESPACO ou ENTER, sem depender do mouse
            return k == Keys.Space || k == Keys.Enter || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                if (Clicavel && Clicou != null) Clicou(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var fundo = Desabilitado ? Paleta.Cartao : CorFundo;
            var borda = Desabilitado ? Color.FromArgb(38, 34, 66) : (Selecionado ? Paleta.Roxo : corBordaTemporaria);
            Desenho.Card(e.Graphics, new Rectangle(0, 0, Width, Height), Raio, fundo, borda, Selecionado ? 2 : 1);
            OnPintou(e.Graphics);
        }

        protected virtual void OnPintou(Graphics g) { }

        public void Titulo(string t, float tam = 11F)
        {
            Controls.Add(new Label
            {
                Text = t,
                Font = new Font("Segoe UI", tam, FontStyle.Bold),
                ForeColor = Paleta.Texto,
                AutoSize = true,
                Location = new Point(18, 14),
                BackColor = Color.Transparent
            });
        }

        public void Sub(string t)
        {
            Controls.Add(new Label
            {
                Text = t,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Paleta.TextoFraco,
                AutoSize = true,
                Location = new Point(18, 38),
                BackColor = Color.Transparent
            });
        }
    }

    // ------------------------------------------------------------
    //  Medidor circular
    // ------------------------------------------------------------
    public class Medidor : Control
    {
        public int Valor = 0;          // 0-100
        public string Legenda = "";
        public string Detalhe = "";
        public Color Cor = Paleta.Roxo;
        public bool MostrarTexto = true;

        public Medidor()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(120, 120);
        }

        public void Atualizar(int v) { Valor = Math.Max(0, Math.Min(100, v)); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int tam = Math.Min(Width, Height) - 12;
            var r = new Rectangle((Width - tam) / 2, (Height - tam) / 2, tam, tam);
            float espessura = Math.Max(6f, tam * 0.085f);
            int pad = (int)espessura + 2;
            var rr = new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2);

            // Trilho
            using (var p = Desenho.Cantos(rr.Width, rr.Height, rr.Width / 2))
            using (var tr = new GraphicsPath())
            {
                tr.AddArc(rr.X, rr.Y, rr.Width, rr.Height, 0, 360);
                using (var pen = new Pen(Color.FromArgb(46, 42, 80), espessura))
                    g.DrawPath(pen, tr);
            }

            // Arco de valor
            float inicio = 135f, total = 270f;
            float ang = total * Valor / 100f;
            using (var p = new GraphicsPath())
            using (var b = new LinearGradientBrush(rr, Color.FromArgb(255, Cor), Paleta.RoxoClaro, 45f))
            {
                p.AddArc(rr.X, rr.Y, rr.Width, rr.Height, inicio, ang);
                g.DrawPath(new Pen(b, espessura) { StartCap = LineCap.Round, EndCap = LineCap.Round }, p);
            }

            if (MostrarTexto)
            {
                using (var f1 = new Font("Segoe UI", (float)tam * 0.20f, FontStyle.Bold))
                using (var f2 = new Font("Segoe UI", (float)tam * 0.085f))
                {
                    var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    int meio = rr.Y + rr.Height / 2;
                    g.DrawString(Valor + "%", f1, new SolidBrush(Paleta.Texto),
                        new Rectangle(rr.X, meio - (int)(tam * 0.17f), rr.Width, (int)(tam * 0.34f)), st);
                    if (Legenda != "")
                        g.DrawString(Legenda, f2, new SolidBrush(Paleta.TextoMudo),
                            new Rectangle(rr.X, meio + (int)(tam * 0.08f), rr.Width, (int)(tam * 0.16f)), st);
                }
            }

            if (Detalhe != "")
                using (var f3 = new Font("Segoe UI", 8F))
                {
                    var st = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(Detalhe, f3, new SolidBrush(Paleta.TextoFraco),
                        new Rectangle(0, Height - 16, Width, 16), st);
                }
        }
    }

    // ------------------------------------------------------------
    //  Item da barra lateral
    // ------------------------------------------------------------
    public class ItemNav : Control
    {
        public string Icone = "";
        public string Rotulo = "";
        public bool Ativo = false;
        public event EventHandler Selecionou;

        public ItemNav()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(64, 58);
        }

        protected override void OnMouseEnter(EventArgs e) { Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Selecionou != null) Selecionou(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Ativo)
            {
                var r = new Rectangle(0, 0, Width, Height);
                using (var br = new LinearGradientBrush(r, Color.FromArgb(70, Paleta.Roxo),
                                                       Color.FromArgb(20, Paleta.Roxo),
                                                       LinearGradientMode.Horizontal))
                using (var p = Desenho.Cantos(r.Width, r.Height, 12))
                    g.FillPath(br, p);
                using (var p = Desenho.Cantos(r.Width, r.Height, 12))
                using (var pen = new Pen(Paleta.Roxo, 1))
                    g.DrawPath(pen, p);
                using (var b = new SolidBrush(Paleta.Roxo))
                    g.FillRectangle(b, 0, 10, 3, Height - 20);
            }

            var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            Color c = Ativo ? Paleta.Texto : Paleta.TextoFraco;

            using (var f = new Font("Segoe UI Symbol", 17F))
                g.DrawString(Icone, f, new SolidBrush(c), new Rectangle(0, 8, Width, 26), st);
            using (var f = new Font("Segoe UI", 7F))
                g.DrawString(Rotulo, f, new SolidBrush(c), new Rectangle(0, 33, Width, 16), st);
        }
    }

    // ------------------------------------------------------------
    //  Etiqueta colorida
    // ------------------------------------------------------------
    public class Etiqueta : Control
    {
        public string Texto = "";
        public Color Cor = Paleta.Roxo;

        public Etiqueta()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(80, 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width, Height);
            Desenho.Card(g, r, Height / 2, Color.FromArgb(48, Cor), Color.FromArgb(110, Cor));
            using (var f = new Font("Segoe UI", 8F, FontStyle.Bold))
            using (var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(Texto, f, new SolidBrush(Cor), r, st);
        }
    }

    // ------------------------------------------------------------
    //  Caixa de selecao (usada nas categorias do perfil)
    // ------------------------------------------------------------
    public class Caixa : Control
    {
        public string Texto = "";
        public bool Marcada = false;
        public string Detalhe = "";
        public event EventHandler Mudou;

        public Caixa()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(300, 26);
            TabStop = true;
        }

        protected override bool IsInputKey(Keys k) { return k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) { Alternar(); e.Handled = true; }
        }

        public void Alternar()
        {
            Marcada = !Marcada;
            Invalidate();
            if (Mudou != null) Mudou(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Alternar();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int y = (Height - 16) / 2;
            var r = new Rectangle(2, y, 16, 16);
            Desenho.Card(g, r, 5, Marcada ? Paleta.Roxo : Color.FromArgb(20, 17, 42),
                         Marcada ? Paleta.Roxo : Paleta.Borda, 1);
            if (Marcada)
            {
                using (var p = new Pen(Color.White, 2F))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLines(p, new[] {
                        new PointF(r.X + 4, r.Y + 8), new PointF(r.X + 7, r.Y + 11), new PointF(r.X + 12, r.Y + 5) });
                }
            }
            // O detalhe vai na MESMA linha do titulo. Antes ficava em baixo e
            // sobrepunha a linha seguinte da lista de categorias.
            using (var f = new Font("Segoe UI", 9F))
            using (var brocha = new SolidBrush(Paleta.Texto))
            {
                g.DrawString(Texto, f, brocha, 26, y - 1);
                if (Detalhe != "")
                {
                    float larg = g.MeasureString(Texto, f).Width;
                    using (var f2 = new Font("Segoe UI", 7.6F))
                    using (var b2 = new SolidBrush(Paleta.TextoFraco))
                        g.DrawString(Detalhe, f2, b2, 32 + larg, y + 2);
                }
            }
        }
    }
    // ------------------------------------------------------------
    //  Botao no estilo do painel
    // ------------------------------------------------------------
    public class Botao : Control
    {
        public string Texto = "";
        public Color CorFundo = Paleta.Roxo;
        public Color CorHover = Paleta.RoxoClaro;
        public bool Contornado = false;
        public event EventHandler Clicou;

        public Botao()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(160, 34);
        }

        protected override void OnMouseEnter(EventArgs e) { Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Clicou != null) Clicou(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width, Height);
            bool sobre = Bounds.Contains(PointToClient(MousePosition));
            Color c = sobre ? CorHover : CorFundo;
            if (Contornado)
                Desenho.Card(g, r, 9, Color.FromArgb(30, c), Color.FromArgb(150, c));
            else
                Desenho.Card(g, r, 9, c, Color.Transparent, 0);

            using (var f = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(Texto, f, Brushes.White, r, st);
        }
    }

    // ------------------------------------------------------------
    //  Icones desenhados na hora
    // ------------------------------------------------------------
    public static class Icones
    {
        public static Image Desenhar(string glifo, int tam, Color cor)
        {
            var b = new Bitmap(tam, tam);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var f = new Font("Segoe UI Symbol", tam * 0.78f, FontStyle.Bold))
                using (var br = new SolidBrush(cor))
                using (var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(glifo, f, br, new Rectangle(0, 0, tam, tam), st);
            }
            return b;
        }

        public static Image Pasta(int tam)
        {
            var b = new Bitmap(tam, tam);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new LinearGradientBrush(new Rectangle(2, 5, tam - 4, tam - 8),
                                                           Paleta.Roxo, Paleta.Azul, LinearGradientMode.ForwardDiagonal))
                using (var p = Desenho.Cantos(tam - 4, tam - 8, 3))
                    g.FillPath(br, p);
                using (var br = new SolidBrush(Paleta.RoxoClaro))
                    g.FillRectangle(br, 2, 2, tam / 2, 5);
            }
            return b;
        }

        public static Image Info(int tam)
        {
            var b = new Bitmap(tam, tam);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(Paleta.Cyan))
                    g.FillEllipse(br, 1, 1, tam - 2, tam - 2);
                using (var f = new Font("Segoe UI", tam * 0.62f, FontStyle.Bold))
                using (var brocha = new SolidBrush(Paleta.FundoA))
                using (var st = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("i", f, brocha, new Rectangle(0, 1, tam, tam), st);
            }
            return b;
        }
    }
    // ------------------------------------------------------------
    //  Ponto de entrada
    // ------------------------------------------------------------
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}




