using System;

namespace GIFImage.WinUI.Core
{
    public class NeuQuant
    {
        protected static readonly int NET_SIZE = 256;
        protected static readonly int PRIME_1 = 499;
        protected static readonly int PRIME_2 = 491;
        protected static readonly int PRIME_3 = 487;
        protected static readonly int PRIME_4 = 503;
        protected static readonly int MIN_PICTURE_BYTES = 3 * PRIME_4;

        protected static readonly int MAX_NETPOS = NET_SIZE - 1;
        protected static readonly int NET_BIAS_SHIFT = 4;
        protected static readonly int NCYCLES = 100;

        protected static readonly int INT_BIAS_SHIFT = 16;
        protected static readonly int INT_BIAS = 1 << INT_BIAS_SHIFT;
        protected static readonly int GAMMA_SHIFT = 10;
        protected static readonly int GAMMA = 1 << GAMMA_SHIFT;
        protected static readonly int BETA_SHIFT = 10;
        protected static readonly int BETA = INT_BIAS >> BETA_SHIFT;
        protected static readonly int BETA_GAMMA = INT_BIAS << GAMMA_SHIFT - BETA_SHIFT;

        protected static readonly int INI_TRAD = NET_SIZE >> 3;
        protected static readonly int RADIUS_BIAS_SHIFT = 6;
        protected static readonly int RADIUS_BIAS = 1 << RADIUS_BIAS_SHIFT;
        protected static readonly int INIT_RADIUS = INI_TRAD * RADIUS_BIAS;
        protected static readonly int RADIUS_DEC = 30;

        protected static readonly int ALPHA_BIAS_SHIFT = 10;
        protected static readonly int INIT_ALPHA = 1 << ALPHA_BIAS_SHIFT;

        protected int ALPHA_DEC;

        protected static readonly int RAD_BIAS_SHIFT = 8;
        protected static readonly int RAD_BIAS = 1 << RAD_BIAS_SHIFT;
        protected static readonly int ALPHA_RAD_BIAS_SHIFT = ALPHA_BIAS_SHIFT + RAD_BIAS_SHIFT;
        protected static readonly int ALPHA_RAD_BIAS = 1 << ALPHA_RAD_BIAS_SHIFT;

        protected byte[] thepicture;
        protected int lengthcount;
        protected int samplefac;

        protected int[][] network;
        protected int[] netindex = new int[256];
        protected int[] bias = new int[NET_SIZE];
        protected int[] freq = new int[NET_SIZE];
        protected int[] radpower = new int[INI_TRAD];

        public NeuQuant(byte[] thepic, int len, int sample)
        {
            thepicture = thepic;
            lengthcount = len;
            samplefac = sample;

            network = new int[NET_SIZE][];
            for (int i = 0; i < NET_SIZE; i++)
            {
                network[i] = new int[4];
                int[] p = network[i];
                p[0] = p[1] = p[2] = (i << NET_BIAS_SHIFT + 8) / NET_SIZE;
                freq[i] = INT_BIAS / NET_SIZE;
                bias[i] = 0;
            }
        }

        public byte[] Process()
        {
            Learn();
            Unbiasnet();
            Inxbuild();
            return ColorMap();
        }

        public byte[] ColorMap()
        {
            byte[] map = new byte[3 * NET_SIZE];
            int[] index = new int[NET_SIZE];
            for (int i = 0; i < NET_SIZE; i++) index[network[i][3]] = i;
            int k = 0;
            for (int i = 0; i < NET_SIZE; i++)
            {
                int j = index[i];
                map[k++] = (byte)network[j][0];
                map[k++] = (byte)network[j][1];
                map[k++] = (byte)network[j][2];
            }
            return map;
        }

        public void Inxbuild()
        {
            int previouscol = 0, startpos = 0;
            for (int i = 0; i < NET_SIZE; i++)
            {
                int[] p = network[i];
                int smallpos = i;
                int smallval = p[1];
                for (int j = i + 1; j < NET_SIZE; j++)
                {
                    int[] q = network[j];
                    if (q[1] < smallval) { smallpos = j; smallval = q[1]; }
                }
                int[] q2 = network[smallpos];
                if (i != smallpos)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int t = p[k];
                        p[k] = q2[k];
                        q2[k] = t;
                    }
                }
                if (smallval != previouscol)
                {
                    netindex[previouscol] = startpos + i >> 1;
                    for (int j = previouscol + 1; j < smallval; j++)
                        netindex[j] = i;
                    previouscol = smallval;
                    startpos = i;
                }
            }
            netindex[previouscol] = startpos + MAX_NETPOS >> 1;
            for (int j = previouscol + 1; j < 256; j++) netindex[j] = MAX_NETPOS;
        }

        public void Learn()
        {
            int step;
            if (lengthcount < MIN_PICTURE_BYTES) samplefac = 1;
            ALPHA_DEC = 30 + (samplefac - 1) / 3;
            int samplepixels = lengthcount / (3 * samplefac);
            int delta = samplepixels / NCYCLES;
            int alpha = INIT_ALPHA;
            int radius = INIT_RADIUS;
            int rad = radius >> RADIUS_BIAS_SHIFT;
            if (rad <= 1) rad = 0;
            for (int i = 0; i < rad; i++)
                radpower[i] = alpha * (rad * rad - i * i) * RAD_BIAS / (rad * rad);

            if (lengthcount < MIN_PICTURE_BYTES) step = 3;
            else if (lengthcount % PRIME_1 != 0) step = 3 * PRIME_1;
            else if (lengthcount % PRIME_2 != 0) step = 3 * PRIME_2;
            else if (lengthcount % PRIME_3 != 0) step = 3 * PRIME_3;
            else step = 3 * PRIME_4;

            int pix = 0;
            for (int i = 0; i < samplepixels; i++)
            {
                int b = (thepicture[pix + 0] & 0xff) << NET_BIAS_SHIFT;
                int g = (thepicture[pix + 1] & 0xff) << NET_BIAS_SHIFT;
                int r = (thepicture[pix + 2] & 0xff) << NET_BIAS_SHIFT;
                int j = Contest(b, g, r);
                Altersingle(alpha, j, b, g, r);
                if (rad != 0) Alterneigh(rad, j, b, g, r);
                pix += step;
                if (pix >= lengthcount) pix -= lengthcount;
                if (delta == 0) delta = 1;
                if (i % delta == 0)
                {
                    alpha -= alpha / ALPHA_DEC;
                    radius -= radius / RADIUS_DEC;
                    rad = radius >> RADIUS_BIAS_SHIFT;
                    if (rad <= 1) rad = 0;
                    for (j = 0; j < rad; j++)
                        radpower[j] = alpha * (rad * rad - j * j) * RAD_BIAS / (rad * rad);
                }
            }
        }

        public void Unbiasnet()
        {
            for (int i = 0; i < NET_SIZE; i++)
            {
                network[i][0] >>= NET_BIAS_SHIFT;
                network[i][1] >>= NET_BIAS_SHIFT;
                network[i][2] >>= NET_BIAS_SHIFT;
                network[i][3] = i;
            }
        }

        public int Map(int b, int g, int r)
        {
            int bestd = int.MaxValue;
            int best = -1;
            int i = netindex[g];
            int j = i - 1;
            while (i < NET_SIZE || j >= 0)
            {
                if (i < NET_SIZE)
                {
                    int[] p = network[i];
                    int dist = Math.Abs(p[1] - g);
                    if (dist >= bestd) break;
                    dist += Math.Abs(p[0] - b);
                    dist += Math.Abs(p[2] - r);
                    if (dist < bestd) { bestd = dist; best = p[3]; }
                    i++;
                }
                if (j >= 0)
                {
                    int[] p = network[j];
                    int dist = Math.Abs(g - p[1]);
                    if (dist >= bestd) break;
                    dist += Math.Abs(p[0] - b);
                    dist += Math.Abs(p[2] - r);
                    if (dist < bestd) { bestd = dist; best = p[3]; }
                    j--;
                }
            }
            return best;
        }

        protected void Altersingle(int alpha, int i, int b, int g, int r)
        {
            int[] n = network[i];
            n[0] -= alpha * (n[0] - b) / INIT_ALPHA;
            n[1] -= alpha * (n[1] - g) / INIT_ALPHA;
            n[2] -= alpha * (n[2] - r) / INIT_ALPHA;
        }

        protected void Alterneigh(int rad, int i, int b, int g, int r)
        {
            int lo = Math.Max(i - rad, -1);
            int hi = Math.Min(i + rad, NET_SIZE);
            int j = i + 1;
            int k = i - 1;
            int m = 1;
            while (j < hi || k > lo)
            {
                int a = radpower[m++];
                if (j < hi)
                {
                    int[] p = network[j++];
                    p[0] -= a * (p[0] - b) / ALPHA_RAD_BIAS;
                    p[1] -= a * (p[1] - g) / ALPHA_RAD_BIAS;
                    p[2] -= a * (p[2] - r) / ALPHA_RAD_BIAS;
                }
                if (k > lo)
                {
                    int[] p = network[k--];
                    p[0] -= a * (p[0] - b) / ALPHA_RAD_BIAS;
                    p[1] -= a * (p[1] - g) / ALPHA_RAD_BIAS;
                    p[2] -= a * (p[2] - r) / ALPHA_RAD_BIAS;
                }
            }
        }

        protected int Contest(int b, int g, int r)
        {
            int bestd = int.MaxValue, bestbiasd = int.MaxValue;
            int bestpos = -1, bestbiaspos = -1;
            for (int i = 0; i < NET_SIZE; i++)
            {
                int[] n = network[i];
                int dist = Math.Abs(n[0] - b) + Math.Abs(n[1] - g) + Math.Abs(n[2] - r);
                if (dist < bestd) { bestd = dist; bestpos = i; }
                int biasdist = dist - (bias[i] >> INT_BIAS_SHIFT - NET_BIAS_SHIFT);
                if (biasdist < bestbiasd) { bestbiasd = biasdist; bestbiaspos = i; }
                int betafreq = freq[i] >> BETA_SHIFT;
                freq[i] -= betafreq;
                bias[i] += betafreq << GAMMA_SHIFT;
            }
            freq[bestpos] += BETA;
            bias[bestpos] -= BETA_GAMMA;
            return bestbiaspos;
        }
    }
}