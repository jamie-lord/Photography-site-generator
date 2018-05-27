using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

            var galleries = new List<Gallery>();

            foreach (DirectoryInfo galleryDir in inputDirectory.EnumerateDirectories())
            {
                Gallery gallery = new Gallery();

                var galleryConfig = galleryDir.GetFiles("gallery.yaml").FirstOrDefault();
                if (galleryConfig != null)
                {
                    using (StreamReader config = galleryConfig.OpenText())
                    {
                        var deserializer = new DeserializerBuilder()
                            .WithNamingConvention(new CamelCaseNamingConvention())
                            .Build();

                        gallery = deserializer.Deserialize<Gallery>(config);
                        galleries.Add(gallery);
                    }
                }

                outputDirectory.CreateSubdirectory(gallery.Uri);

                foreach (FileInfo photoFile in galleryDir.EnumerateFiles())
                {
                    if (photoFile.Extension.ToLower() != ".jpg" && photoFile.Extension.ToLower() != ".jpeg")
                    {
                        continue;
                    }

                    Console.WriteLine($"Processing photo {photoFile.FullName}");
                    try
                    {
                        using (MagickImage image = new MagickImage(photoFile))
                        {
                            var imagePath = Path.Combine(gallery.Uri, photoFile.Name);

                            // Need to get/store metadata here as compressed image losses exif data
                            var exif = image.GetExifProfile();

                            var photograph = new Photograph()
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

                            gallery.Photographs.Add(photograph);

                            string filePath = Path.Combine(outputDirectory.FullName, imagePath);
                            ProcessImage(image, maxImageDimension, filePath);

                            // Need to create a thumbnail image for the gallery index
                            if (gallery.Thumbnail == photoFile.Name)
                            {
                                string thumbnailPath = Path.Combine(outputDirectory.FullName, Path.Combine(gallery.Uri, "thumbnail.jpg"));
                                ProcessImage(image, 1024, thumbnailPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            var indexPage = GetTemplate("index");

            var galleryCards = string.Empty;

            foreach (var gallery in galleries)
            {
                galleryCards += gallery.GenerateIndexCard() + "\n";
            }

            indexPage = InsertContent(indexPage, new Dictionary<string, string> { { "GALLERY_CARDS", galleryCards } });

            File.WriteAllText(Path.Combine(outputDirectory.FullName, "index.html"), indexPage);
        }

        private static void ProcessImage(MagickImage image, int maxImageDimension, string filePath)
        {
            if (image.Width > image.Height)
            {
                image.Resize(maxImageDimension, 0);
            }
            else
            {
                image.Resize(0, maxImageDimension);
            }
            
            image.Write(filePath);
            ImageOptimizer optimizer = new ImageOptimizer();
            optimizer.OptimalCompression = true;
            optimizer.Compress(filePath);
        }

        public static string GetTemplate(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"PhotographySiteGenerator.Templates.{name}.html";
            string result = string.Empty;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }

        public static string InsertContent(string template, Dictionary<string, string> tagContent)
        {
            var result = template;

            foreach (var tag in tagContent)
            {
                try
                {
                    result = result.Replace("{{" + tag.Key + "}}", tag.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return result;
        }
    }

    public class Gallery
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string Location { get; set; }
        public string Thumbnail { get; set; }
        public List<Photograph> Photographs { get; set; } = new List<Photograph>();

        public string GenerateIndexCard()
        {
            var galleryCard = Program.GetTemplate("index_gallery_card");

            galleryCard = Program.InsertContent(galleryCard, new Dictionary<string, string> {
                { "LINK", Uri },
                { "IMAGE", Uri + "/thumbnail.jpg" },
                { "ALT", Name },
                { "TITLE", Name },
                { "DESCRIPTION", Description }
            });

            return galleryCard;
        }
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