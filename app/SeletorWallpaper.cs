using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ModoJogoApp
{
    // Tela que aparece ao clicar em MODO JOGO: escolha do wallpaper.
    // Lista TODAS as imagens da pasta wallpapers, e nao apenas duas fixas.
    public class SeletorWallpaper : Form
    {
        private readonly List<string> caminhos = new List<string>();
        private readonly List<Panel> cards = new List<Panel>();
        private readonly List<Label> legendas = new List<Label>();
        private int selecionado = -1;
        private string resultado = "";

        private static readonly Color Fundo = Color.FromArgb(24, 16, 21);
        private static readonly Color Cartao = Color.FromArgb(38, 26, 33);
        private static readonly Color Ativo = Color.FromArgb(196, 42, 58);
        private static readonly Color Texto = Color.FromArgb(238, 228, 232);
        private static readonly Color Fraco = Color.FromArgb(150, 132, 141);

        public string Escolhido { get { return resultado; } }

        public SeletorWallpaper(string pastaWallpapers)
        {
            if (!Directory.Exists(pastaWallpapers)) Directory.CreateDirectory(pastaWallpapers);
            string[] ext = { ".jpg", ".jpeg", ".png", ".bmp" };
            foreach (var f in Directory.GetFiles(pastaWallpapers))
            {
                string e = Path.GetExtension(f).ToLower();
                if (Array.IndexOf(ext, e) >= 0) caminhos.Add(f);
            }
            caminhos.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (caminhos.Count == 0) resultado = "";
            else resultado = caminhos[0];

            Text = "Escolher wallpaper do Modo Jogo";
            Width = 800;
            Height = 560;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Fundo;
            ForeColor = Texto;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(792, 520);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            var titulo = new Label
            {
                Text = "Qual wallpaper usar no modo jogo?",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Texto, AutoSize = true, Location = new Point(26, 20)
            };
            Controls.Add(titulo);

            var sub = new Label
            {
                Text = "O wallpaper normal volta sozinho quando voce clicar em MODO NORMAL.",
                Font = new Font("Segoe UI", 9F), ForeColor = Fraco, AutoSize = true, Location = new Point(28, 52)
            };
            Controls.Add(sub);

            // grade com scroll: o utilizador pode ter many wallpapers
            var grade = new FlowLayoutPanel
            {
                Location = new Point(20, 84), Size = new Size(752, 250),
                AutoScroll = true, BackColor = Color.FromArgb(18, 12, 16),
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(6)
            };
            Controls.Add(grade);

            for (int i = 0; i < caminhos.Count; i++)
            {
                int idx = i;
                var card = new Panel
                {
                    Size = new Size(232, 150), BackColor = Cartao, Cursor = Cursors.Hand, Margin = new Padding(6)
                };
                var pv = new PictureBox
                {
                    Location = new Point(6, 6), Size = new Size(220, 112),
                    SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(15, 10, 14)
                };
                // Carrega SEM deixar o ficheiro bloqueado (Image.FromFile prendia
                // o .jpg e impedia de o substituir depois).
                pv.Image = Carregar(caminhos[i]);

                var lbl = new Label
                {
                    Text = Path.GetFileNameWithoutExtension(caminhos[i]),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Texto,
                    AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(6, 122), Size = new Size(220, 22), BackColor = Color.Transparent
                };

                // O handler de Paint e ligado UMA VEZ por cartao. Antes era
                // ligado a cada clique e o cartao desenhava o mesmo bordo
                // varias vezes, cada vez mais lento.
                card.Paint += (s, e) => PintarCartao(card, e.Graphics, idx);

                card.Click += (s, e) => Selecionar(idx);
                pv.Click += (s, e) => Selecionar(idx);
                lbl.Click += (s, e) => Selecionar(idx);

                card.Controls.Add(pv);
                card.Controls.Add(lbl);
                grade.Controls.Add(card);
                cards.Add(card);
                legendas.Add(lbl);
            }

            if (caminhos.Count == 0)
            {
                var aviso = new Label
                {
                    Text = "Nenhuma imagem encontrada na pasta wallpapers.\nUse o botao abaixo para escolher um ficheiro.",
                    Font = new Font("Segoe UI", 9.5F), ForeColor = Fraco, AutoSize = false,
                    Location = new Point(20, 100), Size = new Size(752, 44)
                };
                Controls.Add(aviso);
            }

            var btnOutro = new Button
            {
                Text = "Escolher outro ficheiro...",
                Size = new Size(232, 38), Location = new Point(20, 348),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 37, 45),
                ForeColor = Texto, FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand
            };
            btnOutro.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "Escolher wallpaper";
                    dlg.Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp|Todos|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        resultado = dlg.FileName;
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            };
            Controls.Add(btnOutro);

            var btnCancel = new Button
            {
                Text = "Cancelar", Size = new Size(140, 38), Location = new Point(262, 348),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 37, 45),
                ForeColor = Texto, FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            var btnOk = new Button
            {
                Text = "ATIVAR MODO JOGO", Size = new Size(380, 46), Location = new Point(412, 348),
                FlatStyle = FlatStyle.Flat, BackColor = Ativo, ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(resultado)) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnOk);

            if (caminhos.Count > 0) Selecionar(0);
        }

        private static Image Carregar(string caminho)
        {
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(caminho)))
                using (var img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        private void PintarCartao(Panel card, Graphics g, int idx)
        {
            if (idx != selecionado) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(Ativo, 3))
                g.DrawRectangle(p, 1, 1, card.Width - 3, card.Height - 3);
        }

        private void Selecionar(int idx)
        {
            if (idx < 0 || idx >= caminhos.Count) return;
            selecionado = idx;
            resultado = caminhos[idx];
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].BackColor = (i == idx) ? Color.FromArgb(58, 33, 42) : Cartao;
                legendas[i].ForeColor = (i == idx) ? Color.White : Texto;
                cards[i].Invalidate();   // redesenha com o handler ja ligado
            }
        }
    }
}
