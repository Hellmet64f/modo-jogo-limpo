using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Gera um .ico para o atalho: escudo escuro com um "power" vermelho.
class GerarIcone
{
    static void Main(string[] args)
    {
        string saida = args.Length > 0 ? args[0] : "app.ico";
        int[] tamanhos = { 16, 24, 32, 48, 64, 128, 256 };

        using (var img = Desenhar(256))
        using (var ms = new MemoryStream())
        {
            img.Save(ms, ImageFormat.Png);
            byte[] png = ms.ToArray();

            using (var fs = new FileStream(saida, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);                 // reservado
                bw.Write((ushort)1);                 // tipo: icone
                bw.Write((ushort)tamanhos.Length);

                int offset = 6 + 16 * tamanhos.Length;

                // Os maiores primeiro, que e o que o Windows espera.
                for (int i = tamanhos.Length - 1; i >= 0; i--)
                {
                    int t = tamanhos[i];
                    byte[] dados = ConverterParaBmp(img, t);
                    bw.Write((byte)(t >= 256 ? 0 : t));
                    bw.Write((byte)(t >= 256 ? 0 : t));
                    bw.Write((byte)0);              // cores
                    bw.Write((byte)0);              // reservado
                    bw.Write((ushort)1);             // planos
                    bw.Write((ushort)32);            // bits por pixel
                    bw.Write((uint)dados.Length);
                    bw.Write((uint)offset);
                    offset += dados.Length;
                }

                for (int i = tamanhos.Length - 1; i >= 0; i--)
                    bw.Write(ConverterParaBmp(img, tamanhos[i]));
            }
        }
        Console.WriteLine("Icone gerado: " + saida);
    }

    static Image Desenhar(int lado)
    {
        var bmp = new Bitmap(lado, lado);
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Escudo redondo com degrade
        int m = lado / 12;
        var ret = new Rectangle(m, m, lado - 2 * m, lado - 2 * m);
        using (var p = new GraphicsPath())
        {
            p.AddEllipse(ret);
            using (var br = new PathGradientBrush(p))
            {
                br.CenterColor = Color.FromArgb(52, 36, 45);
                br.SurroundColors = new[] { Color.FromArgb(26, 17, 22) };
                g.FillEllipse(br, ret);
            }
        }

        using (var pen = new Pen(Color.FromArgb(205, 48, 64), lado / 48f))
            g.DrawEllipse(pen, m, m, lado - 2 * m, lado - 2 * m);

        // Simbolo de energia
        float cx = lado / 2f, cy = lado / 2f;
        float raio = lado * 0.26f, espessura = lado * 0.085f;
        using (var pen = new Pen(Color.FromArgb(228, 62, 78), espessura))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, cx - raio, cy - raio, raio * 2, raio * 2, 125, 290);
        }
        using (var pen = new Pen(Color.FromArgb(228, 62, 78), espessura))
        {
            pen.StartCap = LineCap.Round;
            g.DrawLine(pen, cx, cy - raio * 1.25f, cx, cy - raio * 0.15f);
        }

        g.Dispose();
        return bmp;
    }

    // BMP 32bpp BGRA de baixo pra cima, sem cabecalho de arquivo.
    static byte[] ConverterParaBmp(Image img, int t)
    {
        using (var b = new Bitmap(t, t, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                g.DrawImage(img, new Rectangle(0, 0, t, t));
            }

            var dados = b.LockBits(new Rectangle(0, 0, t, t),
                                   ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] buf = new byte[Math.Abs(dados.Stride) * t];
            System.Runtime.InteropServices.Marshal.Copy(dados.Scan0, buf, 0, buf.Length);
            b.UnlockBits(dados);

            // ICO espera as linhas de baixo pra cima
            int stride = dados.Stride;
            byte[] invertido = new byte[buf.Length];
            for (int y = 0; y < t; y++)
                Array.Copy(buf, y * stride, invertido, (t - 1 - y) * stride, stride);

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write((uint)40);            // BITMAPINFOHEADER
                    bw.Write(t);
                    bw.Write(t * 2);              // altura x2 (xor + mascara)
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write(0);                  // BI_RGB
                    bw.Write((uint)invertido.Length);
                    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
                    bw.Write(invertido);
                }
                return ms.ToArray();
            }
        }
    }
}