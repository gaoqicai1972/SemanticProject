using DocSharp.Binary.DocFileFormat;
using DocSharp.Binary.StructuredStorage.Reader;
using DocSharp.Docx;

namespace DocToMarkdown
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            var path = @"D:\funacapimd\resources";
            //var path = @"D:\CNC1";

            MassConvertDocToDocx(path);
        }

        private static void MassConvertDocToDocx(string folderPath)
        {
            try
            {
                var files = Directory.GetFiles(folderPath, "*.doc", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string inputExt = Path.GetExtension(file).ToLower();
                    string outputExt = inputExt + "x";
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    var path = Path.GetDirectoryName(file);
                    string outputFile = Path.Join(path, baseName + outputExt);

                    using (var reader = new StructuredStorageReader(file))
                    {
                        switch (inputExt)
                        {
                            case ".doc":
                                var doc = new WordDocument(reader);
                                var docxType = inputExt == ".dot" ? DocSharp.Binary.OpenXmlLib.WordprocessingDocumentType.Template :
                                                                    DocSharp.Binary.OpenXmlLib.WordprocessingDocumentType.Document;
                                using (var docx = DocSharp.Binary.OpenXmlLib.WordprocessingML.WordprocessingDocument.Create(outputFile, docxType))
                                {
                                    DocSharp.Binary.WordprocessingMLMapping.Converter.Convert(doc, docx);
                                }
                                break;
                        }
                    }
                    ConvertDocxToMD(outputFile);
                    Console.WriteLine(outputFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void ConvertDocxToMD(string fileName)
        {
            try
            {
                var outFile = fileName.Replace(".docx", ".md");
                var converter = new DocxToMarkdownConverter()
                {
                    //ImagesOutputFolder = Path.GetDirectoryName(fileName),
                    //ImagesBaseUriOverride = "",
                    //ImageConverter = new SystemDrawingConverter(), // Converts TIFF, WMF and EMF
                    //                                               // (ImageSharp does not support WMF / EMF yet)
                    OriginalFolderPath = Path.GetDirectoryName(outFile) // converts sub-documents (if any)
                };
                converter.Convert(fileName, outFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ConvertDocxToMD:" + ex.Message);
            }
        }

    }
}
