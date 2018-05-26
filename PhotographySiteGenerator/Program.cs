using ImageMagick;
using System;
using System.IO;

namespace PhotographySiteGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DirectoryInfo inputDirectory = new DirectoryInfo(@"D:\Published photos");
            DirectoryInfo outputDirectory = new DirectoryInfo(@"D:\Photography site output");

            int maxImageDimension = 2560;

            foreach (DirectoryInfo galleryDir in inputDirectory.EnumerateDirectories())
            {
                outputDirectory.CreateSubdirectory(galleryDir.Name);

                foreach (FileInfo photo in galleryDir.EnumerateFiles())
                {
                    if (photo.Extension.ToLower() != ".jpg" && photo.Extension.ToLower() != ".jpeg")
                    {
                        continue;
                    }

                    Console.WriteLine($"Processing photo {photo.FullName}");
                    try
                    {
                        using (MagickImage image = new MagickImage(photo))
                        {
                            if (image.Width > image.Height)
                            {
                                image.Resize(maxImageDimension, 0);
                            }
                            else
                            {
                                image.Resize(0, maxImageDimension);
                            }

                            string filePath = Path.Combine(outputDirectory.FullName, galleryDir.Name, photo.Name);
                            image.Write(filePath);

                            ImageOptimizer optimizer = new ImageOptimizer();
                            optimizer.OptimalCompression = true;
                            optimizer.Compress(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
        }
    }
}
 