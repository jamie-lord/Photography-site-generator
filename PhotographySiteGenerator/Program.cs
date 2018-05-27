using ImageMagick;
using System;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PhotographySiteGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DirectoryInfo inputDirectory = new DirectoryInfo(@"D:\Published photos");
            DirectoryInfo outputDirectory = new DirectoryInfo(@"D:\Photography site output");

            foreach (FileInfo file in outputDirectory.EnumerateFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in outputDirectory.EnumerateDirectories())
            {
                dir.Delete(true);
            }

            int maxImageDimension = 2560;

            foreach (DirectoryInfo galleryDir in inputDirectory.EnumerateDirectories())
            {
                var galleryConfig = galleryDir.GetFiles("gallery.yaml").FirstOrDefault();
                if (galleryConfig != null)
                {
                    using (StreamReader config = galleryConfig.OpenText())
                    {
                        var deserializer = new DeserializerBuilder()
                            .WithNamingConvention(new CamelCaseNamingConvention())
                            .Build();

                        var gallery = deserializer.Deserialize<Gallery>(config);
                    }
                }

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
                            var imagePath = Path.Combine(galleryDir.Name, photo.Name);

                            // Need to get/store metadata here as compressed image losses exif data
                            var exif = image.GetExifProfile();

                            var p = new Photograph()
                            {
                                Path = imagePath,
                                DateTime = exif.GetValue(ExifTag.DateTimeOriginal).Value?.ToString(),
                                Camera = exif.GetValue(ExifTag.Make).Value?.ToString() + " " + exif.GetValue(ExifTag.Model).Value?.ToString(),
                                Lens = exif.GetValue(ExifTag.LensModel).Value?.ToString(),
                                Fstop = exif.GetValue(ExifTag.FNumber).Value?.ToString(),
                                Exposure = exif.GetValue(ExifTag.ExposureTime).Value?.ToString(),
                                FocalLength = exif.GetValue(ExifTag.FocalLength).Value?.ToString(),
                                Iso = exif.GetValue(ExifTag.ISOSpeedRatings).Value?.ToString()
                            };

                            if (image.Width > image.Height)
                            {
                                image.Resize(maxImageDimension, 0);
                            }
                            else
                            {
                                image.Resize(0, maxImageDimension);
                            }

                            string filePath = Path.Combine(outputDirectory.FullName, imagePath);
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

    public class Gallery
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string Location { get; set; }
    }

    public class Photograph
    {
        public string Path { get; set; }
        public string DateTime { get; set; }
        public string Camera { get; set; }
        public string Lens { get; set; }
        public string Fstop { get; set; }
        public string Exposure { get; set; }
        public string Iso { get; set; }
        public string FocalLength { get; set; }
    }
}