using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Media = System.Windows.Media;
using MediaImaging = System.Windows.Media.Imaging;
using WpfImaging = System.Windows.Interop.Imaging;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace Lightspeed_wpf
{
    public static class IconHelper
    {
        private static readonly Dictionary<string, Media.ImageSource> _cache = new Dictionary<string, Media.ImageSource>();

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const int SHIL_JUMBO = 0x4;
        private const uint ILD_TRANSPARENT = 0x0001;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageListNative ppv);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43EB78CB-9080-4D08-8020-B5C3D0A25B7E")]
        private interface IImageListNative
        {
            void Unused1(); void Unused2(); void Unused3(); void Unused4(); void Unused5();
            void Unused6(); void Unused7(); void Unused8(); void Unused9(); void Unused10();
            void Unused11();
            [PreserveSig]
            int GetIcon(int i, uint flags, out IntPtr picon);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static IntPtr GetJumboIconHandle(int iconIndex)
        {
            try
            {
                var iid = new Guid("43EB78CB-9080-4D08-8020-B5C3D0A25B7E");
                if (SHGetImageList(SHIL_JUMBO, ref iid, out var imageList) == 0 && imageList != null)
                {
                    try
                    {
                        if (imageList.GetIcon(iconIndex, ILD_TRANSPARENT, out var hIcon) == 0 && hIcon != IntPtr.Zero)
                            return hIcon;
                    }
                    finally { Marshal.ReleaseComObject(imageList); }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        public static Media.ImageSource GetIcon(string path, bool isDirectory, int size)
        {
            string cacheKey = $"{path}_{size}";
            if (_cache.ContainsKey(cacheKey))
            {
                return _cache[cacheKey];
            }

            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_SYSICONINDEX;
                uint attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                SHGetFileInfo(path, attributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

                IntPtr hIcon = GetJumboIconHandle(shfi.iIcon);
                bool isJumbo = hIcon != IntPtr.Zero;

                if (!isJumbo)
                {
                    SHFILEINFO shfi2 = new SHFILEINFO();
                    SHGetFileInfo(path, attributes, ref shfi2, (uint)Marshal.SizeOf(shfi2), SHGFI_ICON | SHGFI_LARGEICON);
                    hIcon = shfi2.hIcon;
                }

                if (hIcon != IntPtr.Zero)
                {
                    var managedIcon = Drawing.Icon.FromHandle(hIcon);
                    int drawSize = size;
                    int offset = 0;
                    Media.ImageSource? result = null;

                    using (var bitmap = new Drawing.Bitmap(size, size))
                    using (var graphics = Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.Clear(Drawing.Color.Transparent);
                        graphics.DrawIcon(managedIcon, new Drawing.Rectangle(offset, offset, drawSize, drawSize));

                        var hBitmap = bitmap.GetHbitmap(Drawing.Color.FromArgb(0, 0, 0, 0));
                        try
                        {
                            result = WpfImaging.CreateBitmapSourceFromHBitmap(
                                hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, MediaImaging.BitmapSizeOptions.FromEmptyOptions());
                            result.Freeze();
                        }
                        finally
                        {
                            DeleteObject(hBitmap);
                        }
                    }
                    DestroyIcon(hIcon);

                    if (result != null)
                    {
                        _cache[cacheKey] = result;
                        return result;
                    }
                }
            }
            catch { }

            var defaultIcon = CreateDefaultIcon(isDirectory, size);
            _cache[cacheKey] = defaultIcon;
            return defaultIcon;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static Media.ImageSource CreateDefaultIcon(bool isFolder, int size)
        {
            Media.DrawingVisual drawingVisual = new Media.DrawingVisual();
            using (Media.DrawingContext dc = drawingVisual.RenderOpen())
            {
                if (isFolder)
                {
                    dc.DrawRectangle(new Media.SolidColorBrush(Media.Color.FromRgb(255, 193, 7)), null, new System.Windows.Rect(2, 6, 20, 16));
                    dc.DrawRectangle(new Media.SolidColorBrush(Media.Color.FromRgb(255, 160, 0)), null, new System.Windows.Rect(2, 2, 10, 6));
                }
                else
                {
                    dc.DrawRectangle(new Media.SolidColorBrush(Media.Color.FromRgb(144, 202, 249)), null, new System.Windows.Rect(2, 2, 20, 20));
                }
            }

            MediaImaging.RenderTargetBitmap renderBitmap = new MediaImaging.RenderTargetBitmap(size, size, 96, 96, Media.PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            return renderBitmap;
        }
    }
}
