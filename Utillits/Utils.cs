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
        /// Calculate the fitting size of the image in the container
        /// </summary>  
        public static (double width, double height) CalculateSize(double imageWidth, double imageHeight, double controlWidth, double controlHeight, Stretch mode)
        {
            switch (mode)
            {
                case Stretch.Fill:
                    return (controlWidth, controlHeight); // Forced stretching to fill

                case Stretch.Uniform:
                    double uniformRatio = Math.Min(controlWidth / imageWidth, controlHeight / imageHeight);
                    return (imageWidth * uniformRatio, imageHeight * uniformRatio); // Scale proportionally into the container

                case Stretch.UniformToFill:
                    double fillRatio = Math.Max(controlWidth / imageWidth, controlHeight / imageHeight);
                    return (imageWidth * fillRatio, imageHeight * fillRatio); // Scale proportionally and crop the overflow portion

                default:
                    return (imageWidth, imageHeight); // Default to maintain original size
            }
        }
    }
}
