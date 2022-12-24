using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autofac;
using FluentAssertions;
using NUnit.Framework;
using TagsCloudContainer.Algorithm;
using TagsCloudContainer.Extensions;
using TagsCloudContainer.Infrastructure;
using TagsCloudContainer.Visualisator;

namespace TagCloudContainerTests
{
    [TestFixture]
    public class Tests
    {
        private string PathToProj = Environment.CurrentDirectory;
        private ImageSettings imageSettings;
        private AlgorithmSettings algoSettings;
        private FileSettings fileSettings;
        private ICloudLayouter cloudLayouter;
        private IParser parser;
        private PictureBox pictureBox;
        private IPainter painter;
        private IWordsCounter wordsCounter;


        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            TagsCloudContainer.Program.RegisterDependencies(builder);
            var container = builder.Build();
            imageSettings = container.Resolve<ImageSettings>();
            algoSettings = container.Resolve<AlgorithmSettings>();
            fileSettings = container.Resolve<FileSettings>();
            fileSettings.SourceFilePath = PathToProj + @"\source.txt";
            fileSettings.CustomBoringWordsFilePath = PathToProj + @"\boring.txt";
            fileSettings.ResultImagePath = PathToProj + @"\image.png";
            parser = container.Resolve<IParser>();
            cloudLayouter = container.Resolve<ICloudLayouter>();
            pictureBox = container.Resolve<PictureBox>();
            painter = container.Resolve<IPainter>();
            wordsCounter = container.Resolve<IWordsCounter>();
        }

        [Test]
        public void ParserReturnsResultFail_WhenFileDontExist()
        {
            var path = PathToProj + "rnd.asd";
            if (File.Exists(path))
                File.Delete(path);
            var wordsCountResult = parser.CountWordsInFile(path);
            var wordsResult = parser.FindWordsInFile(path);

            wordsCountResult.IsSuccess.Should().BeFalse();
            wordsCountResult.Error.Should().Be("����� �� ����������");
            wordsResult.IsSuccess.Should().BeFalse();
            wordsResult.Error.Should().Be("����� �� ����������");
        }

        [Test]
        public void ParserReturnsResultFail_WhenWhiteSpacesInWords()
        {
            FillSourceFile("source.txt", new[] { "�� ���" });
            FillSourceFile("boring.txt", new[] { "���� ���" });
            var wordsCountResult = parser.CountWordsInFile(fileSettings.SourceFilePath);
            var wordsResult = parser.FindWordsInFile(fileSettings.CustomBoringWordsFilePath);

            wordsCountResult.IsSuccess.Should().BeFalse();
            wordsCountResult.Error.Should().Be("���� ����������� (�������� ������� � ������)");
            wordsResult.IsSuccess.Should().BeFalse();
            wordsResult.Error.Should().Be("���� ����������� (�������� ������� � ������)");
        }

        [Test]
        public void WordsCounterResultErrorsContainsRefines_WhenFails()
        {
            FillSourceFile("source.txt", new[] { "�� ���" });
            FillSourceFile("boring.txt", new[] { "���� ���" });
            var wordsResult = wordsCounter.CountWords(fileSettings.SourceFilePath,
                fileSettings.CustomBoringWordsFilePath);

            wordsResult.IsSuccess.Should().BeFalse();
            wordsResult.Error.Should().ContainAll(new[] {"�������� ����", "�������� ������� ����" });
        }

        [TestCase(new object[] {"a","a"}, 1)]
        [TestCase(new object[] { "a", "b" }, 2)]
        [TestCase(new object[] { "a", "a", "b" }, 2)]
        public void ParserCountAllWordsInFile_WhenCustomBoringIsEmpty(object[] sourceFileText, int expectedCount)
        {
            FillSourceFile("source.txt", sourceFileText.Cast<string>());
            var res = parser.CountWordsInFile(fileSettings.SourceFilePath);

            res.Value.Count.Should().Be(expectedCount);
        }

        [TestCase(new object[] { "�����", "a" }, 1)]
        [TestCase(new object[] { "�����", "1" }, 1)]
        [TestCase(new object[] { "�����", "����" }, 1)]
        [TestCase(new object[] { "�����", "�" }, 1)]
        [TestCase(new object[] { "�����", "�" }, 1)]
        [TestCase(new object[] { "�����", "��" }, 1)]
        public void WordsCounterNotCount_SimpleBoringWords(object[] sourceFileText, int expectedCount)
        {
            FillSourceFile("source.txt", sourceFileText.Cast<string>());
            FillSourceFile("boring.txt", Array.Empty<string>());
            var res = wordsCounter.CountWords(fileSettings.SourceFilePath, 
                fileSettings.CustomBoringWordsFilePath);

            res.Value.Count.Should().Be(expectedCount);
        }

        [Test]
        public void WordsCounterNotCount_CustomBoringWords()
        {
            FillSourceFile("source.txt", new[] { "�����", "�������" });
            FillSourceFile("boring.txt", new[] { "�����" });
            var res = wordsCounter.CountWords(fileSettings.SourceFilePath,
                fileSettings.CustomBoringWordsFilePath);

            res.Value.Count.Should().Be(1);
        }

        [Test]
        public void ParserTrimmingAndLoweringWords()
        {
            FillSourceFile("source.txt", new[] { " ����� ", "�E�����" });
            FillSourceFile("boring.txt", new[] { "\t������� " });
            var res1 = parser.CountWordsInFile(fileSettings.SourceFilePath);
            var res2 = parser.FindWordsInFile(fileSettings.CustomBoringWordsFilePath);

            res1.Value.ContainsKey("�����").Should().BeTrue();
            res1.Value.ContainsKey("�e�����").Should().BeTrue();
            res2.Value.Contains("�������").Should().BeTrue();
        }

        [TestCase(new object[0], new object[0], 0)]
        [TestCase(new object[] { "�����", "�������" }, new object[0], 2)]
        [TestCase(new object[] { "�����", "�����" }, new object[0], 1)]
        [TestCase(new object[] { "�����", "�������" }, new object[] { "�������" }, 1)]
        public void LayouterFindRightCountOfRectangles(object[] sourceFileText, object[] boringWordsFileText, int expected)
        {
            FillSourceFile("source.txt", sourceFileText.Cast<string>());
            FillSourceFile("boring.txt", boringWordsFileText.Cast<string>());
            var res = GetRectangles();

            res.Count.Should().Be(expected);
        }
        
        [TestCase(new object[] {"�����", "�������"}, new object[] { 1.0, 1.0 })]
        [TestCase(new object[] {"�����", "�����", "�������"}, new object[] { 2, 1 })]
        [TestCase(new object[] {"�����", "�����", "�����", "�������", "�������", "�������", "���", "���"}
            , new object[] { 3, 3, 2})]
        public void LayouterRightCalculateRectangleSize(object[] sourceFileText, object[] expectedRatio)
        {
            FillSourceFile("source.txt", sourceFileText.Cast<string>());
            FillSourceFile("boring.txt", Array.Empty<string>());
            var res = GetRectangles();

            var squares = res
                .Select(e => Convert.ToDouble(e.rectangle.Width * e.rectangle.Height))
                .ToArray();
            var squaresSum = squares.Sum();
            var expected = expectedRatio
                .Select(e => Math.Pow(Convert.ToDouble(e), 2))
                .ToArray();
            var expectedSum = expected.Sum();
            for (int i = 0; i < squares.Length; i++)
            {
                var ratio = squares[i] / squaresSum;
                var exp = expected[i] / expectedSum;
                (Math.Abs(ratio - exp)).Should().BeLessThan(0.1);
            }
        }

        [Test]
        public void PainterSaveImage_CreateFile()
        {
            if (File.Exists(fileSettings.ResultImagePath))
                File.Delete(fileSettings.ResultImagePath);

            pictureBox.RecreateImage(imageSettings);
            painter.Paint(GetRectangles());
            pictureBox.SaveImage(fileSettings.ResultImagePath);

            File.Exists(fileSettings.ResultImagePath).Should().BeTrue();
        }

        private List<(Rectangle rectangle, string text)> GetRectangles()
        {
            var words = wordsCounter.CountWords(fileSettings.SourceFilePath,
                fileSettings.CustomBoringWordsFilePath);
            return cloudLayouter
                .FindRectanglesPositions(imageSettings.Width, imageSettings.Height, words.Value)
                    .Value;
        }

        private void FillSourceFile(string filename, IEnumerable<string> text)
        {
            var path = PathToProj + $@"\{filename}";
            if (File.Exists(path))
                File.Delete(path);
            using (FileStream fstream = new FileStream(path, FileMode.OpenOrCreate))
            {
                byte[] buffer = Encoding.Default.GetBytes(string.Join("\n", text));
                fstream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
