using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GIFImage.WinUI.Utillits
{
    /// <summary>
    /// linq 等等方法扩展类
    /// </summary>
    internal static class SystemEx
    {
        public static bool IsNotEmpty<T>(this IEnumerable<T> list)
        {
            if (list == null || !list.Any())
                return false;
            return true;
        }

        public static bool IsEmpty<T>(this IEnumerable<T> list)
        {
            return !list.IsNotEmpty();
        }

        public static V GetValue<K, V>(this IDictionary<K, V> dic, K key)
        {
            if (dic == null || !dic.ContainsKey(key))
                return default;
            return dic[key];
        }
    }
}