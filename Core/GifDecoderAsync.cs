using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GIFImage.WinUI.Core
{
    public partial class GifDecoder
    {
        private BufferedStream Parse(string name)
        {
            BufferedStream result = null;
            if (name == null)
                return result;
            name = name.Trim().ToLower();
            if (name.Contains("file:") || name.Contains(":/"))
            {
                result = new BufferedStream(WebRequest.Create(name).GetResponse().GetResponseStream());
            }
            else if (File.Exists(name))
            {
                result = new BufferedStream(new FileStream(name, FileMode.Open, FileAccess.Read));
            }
            return result;
        }
        public IEnumerable<Frame> ReadAsync(string name)
        {
            try
            {
                inStream = Parse(name);
                if (inStream == null)
                    yield break;
            }
            catch
            {
                yield break;
            }
            #region read
            Init();
            if (inStream != null)
            {
                inStream = inStream as BufferedStream ?? new BufferedStream(inStream);
                ReadHeader();
                if (!Err())
                {
                    bool done = false;
                    while (!done && !Err())
                    {
                        int code = ReadByte();
                        switch (code)
                        {
                            case 0x2C:
                                yield return ReadImageAsync();
                                break;
                            case 0x21:
                                code = ReadByte();
                                if (code == 0xf9)
                                {
                                    ReadGraphicControlExt();
                                }
                                else if (code == 0xff)
                                {
                                    ReadBlock();
                                    string app = System.Text.Encoding.ASCII.GetString(block, 0, 11);
                                    if (app == "NETSCAPE2.0")
                                        ReadNetscapeExt();
                                    else
                                        Skip();
                                }
                                else
                                {
                                    Skip();
                                }
                                break;
                            case 0x3b:
                                done = true;
                                break;
                            case 0x00:
                                break;
                            default:
                                status = STATUS_FORMAT_ERROR;
                                break;
                        }
                    }
                    if (frameCount < 0) status = STATUS_FORMAT_ERROR;
                }
            }
            else
            {
                status = STATUS_OPEN_ERROR;
            }
            inStream?.Close();
            #endregion
            yield break;
        }

        protected Frame ReadImageAsync()
        {
            ix = ReadShort();
            iy = ReadShort();
            iw = ReadShort();
            ih = ReadShort();

            int packed = ReadByte();
            lctFlag = (packed & 0x80) != 0;
            interlace = (packed & 0x40) != 0;
            lctSize = 2 << (packed & 7);

            act = lctFlag ? ReadColorTable(lctSize) : gct;
            if (bgIndex == transIndex) bgColor = 0;

            int save = 0;
            if (transparency)
            {
                save = act[transIndex];
                act[transIndex] = 0;
            }

            if (act == null)
            {
                status = STATUS_FORMAT_ERROR;
                return null;
            }

            if (Err()) return null;
            DecodeImageData();
            Skip();
            if (Err()) return null;

            frameCount++;
            currentImage = new();
            currentImage.Width = width;
            currentImage.Height = height;
            currentImage.Delay = delay;
            currentImage.MetaData = GetPixels();
            frames.Add(currentImage);

            if (transparency)
                act[transIndex] = save;

            ResetFrame();
            return currentImage;
        }
    }
}
