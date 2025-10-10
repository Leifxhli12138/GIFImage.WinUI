using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFImage.WinUI.Utillits
{
    internal class Utils
    {
        /// <summary>  
        /// 计算图片在容器中的适配尺寸  
        /// </summary>  
        public static (double width, double height) CalculateSize(double imageWidth, double imageHeight, double controlWidth, double controlHeight, Stretch mode)
        {
            switch (mode)
            {
                case Stretch.Fill:
                    return (controlWidth, controlHeight); // 强制拉伸填满  

                case Stretch.Uniform:
                    double uniformRatio = Math.Min(controlWidth / imageWidth, controlHeight / imageHeight);
                    return (imageWidth * uniformRatio, imageHeight * uniformRatio); // 等比例缩放至容器内  

                case Stretch.UniformToFill:
                    double fillRatio = Math.Max(controlWidth / imageWidth, controlHeight / imageHeight);
                    return (imageWidth * fillRatio, imageHeight * fillRatio); // 等比例缩放并裁剪溢出部分  

                default:
                    return (imageWidth, imageHeight); // 默认保持原尺寸  
            }
        }
    }
}
