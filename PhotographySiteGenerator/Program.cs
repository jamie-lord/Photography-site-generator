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
        private static readonly DirectoryInfo _outputDirectory = new DirectoryInfo(@"D:\Photography site output");

        public static void Main(string[] args)
        {
            DirectoryInfo inputDirectory = new DirectoryInfo(@"D:\Published photos");

            foreach (FileInfo file in _outputDirectory.EnumerateFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in _outputDirectory.EnumerateDirectories())
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

                _outputDirectory.CreateSubdirectory(gallery.Uri);

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
                            var photograph = new Photograph()
                            {
                                FileName = photoFile.Name
                            };
                            photograph.ParseMetadata(image.GetExifProfile());

                            gallery.Photographs.Add(photograph);

                            string filePath = Path.Combine(_outputDirectory.FullName, gallery.Uri, photoFile.Name);
                            ProcessImage(image, maxImageDimension, filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            MakeIndex(galleries);

            foreach (var gallery in galleries)
            {
                MakeGallery(gallery);
            }
        }

        private static void MakeIndex(IEnumerable<Gallery> galleries)
        {
            var indexPage = GetTemplate("index");

            var galleryCards = string.Empty;

            foreach (var gallery in galleries)
            {
                galleryCards += gallery.GenerateIndexCard() + "\n";
            }

            indexPage = InsertContent(indexPage, new Dictionary<string, string> { { "GALLERY_CARDS", galleryCards } });

            File.WriteAllText(Path.Combine(_outputDirectory.FullName, "index.html"), indexPage);
        }

        private static void MakeGallery(Gallery gallery)
        {
            var galleryPage = GetTemplate("gallery");

            var galleryCards = string.Empty;

            foreach (var photo in gallery.Photographs)
            {
                galleryCards += photo.GenerateGalleryCard();
            }

            galleryPage = InsertContent(galleryPage, new Dictionary<string, string> { { "IMAGE_CARDS", galleryCards } });

            File.WriteAllText(Path.Combine(_outputDirectory.FullName, gallery.Uri, "index.html"), galleryPage);
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
            var indexGalleryCard = Program.GetTemplate("index_gallery_card");

            indexGalleryCard = Program.InsertContent(indexGalleryCard, new Dictionary<string, string> {
                { "LINK", Uri },
                { "IMAGE", Uri + "/" + Thumbnail },
                { "ALT", Name },
                { "TITLE", Name },
                { "DESCRIPTION", Description }
            });

            return indexGalleryCard;
        }
    }

    public class Photograph
    {
        public string FileName { get; set; }
        public string DateTime { get; set; }
        public string Camera { get; set; }
        public string Lens { get; set; }
        public string Fstop { get; set; }
        public string Exposure { get; set; }
        public string Iso { get; set; }
        public string FocalLength { get; set; }

        public string GenerateGalleryCard()
        {
            var galleryCard = Program.GetTemplate("gallery_card");

            galleryCard = Program.InsertContent(galleryCard, new Dictionary<string, string>
            {
                { "IMAGE_URI", FileName }
            });

            return galleryCard;
        }

        public void ParseMetadata(ExifProfile exifProfile)
        {
            DateTime = TryParseExif(exifProfile, ExifTag.DateTimeOriginal);
            Camera = TryParseExif(exifProfile, ExifTag.Make) + " " + TryParseExif(exifProfile, ExifTag.Model);
            Lens = TryParseExif(exifProfile, ExifTag.LensModel);
            Fstop = TryParseExif(exifProfile, ExifTag.FNumber);
            Exposure = TryParseExif(exifProfile, ExifTag.ExposureTime);
            FocalLength = TryParseExif(exifProfile, ExifTag.FocalLength);
            Iso = TryParseExif(exifProfile, ExifTag.ISOSpeedRatings);
        }

        private string TryParseExif(ExifProfile exifProfile, ExifTag exifTag)
        {
            var result = string.Empty;
            try
            {
                result = exifProfile.GetValue(exifTag).Value?.ToString();
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex);
            }
            return result;
        }
    }
}