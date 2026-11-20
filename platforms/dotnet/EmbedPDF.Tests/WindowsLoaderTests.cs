using Xunit;
namespace EmbedPDF.Tests;

public class WindowsLoaderTests
{
    [Fact]
    public void SourceTestsStageDependencyWhereProductionPreloadLooks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "epdf-windows-staging-" + Guid.NewGuid());
        var runtime = Path.Combine(directory, "runtime");
        var output = Path.Combine(directory, "output");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(output);
        try
        {
            byte[] dependency = [1, 2, 3, 4];
            File.WriteAllBytes(Path.Combine(runtime, "embedpdf.dll"), dependency);
            FacadeTests.StageWindowsDependency(runtime, output);
            Assert.Equal(dependency, File.ReadAllBytes(Path.Combine(output, "embedpdf.dll")));
            File.Delete(Path.Combine(runtime, "embedpdf.dll"));
            Assert.Throws<FileNotFoundException>(() => FacadeTests.StageWindowsDependency(runtime, output));
        }
        finally { Directory.Delete(directory, true); }
    }

    // A child test process stages into the output directory the parent host already loaded
    // embedpdf.dll from; Windows refuses to overwrite a loaded image, so identical bytes must not be rewritten.
    [Fact]
    public void StagingCopiesOnlyWhenTheDependencyDiffers()
    {
        var directory = Path.Combine(Path.GetTempPath(), "epdf-windows-staging-" + Guid.NewGuid());
        var runtime = Path.Combine(directory, "runtime");
        var output = Path.Combine(directory, "output");
        var source = Path.Combine(runtime, "embedpdf.dll");
        var staged = Path.Combine(output, "embedpdf.dll");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(output);
        try
        {
            byte[] dependency = [1, 2, 3, 4];
            File.WriteAllBytes(source, dependency);
            FacadeTests.StageWindowsDependency(runtime, output);
            Assert.Equal(dependency, File.ReadAllBytes(staged));

            var stamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(staged, stamp);
            using (new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.Read))
                FacadeTests.StageWindowsDependency(runtime, output);
            Assert.Equal(stamp, File.GetLastWriteTimeUtc(staged));

            byte[] sameLength = [4, 3, 2, 1];
            File.WriteAllBytes(source, sameLength);
            FacadeTests.StageWindowsDependency(runtime, output);
            Assert.Equal(sameLength, File.ReadAllBytes(staged));

            byte[] longer = [5, 6, 7, 8, 9];
            File.WriteAllBytes(source, longer);
            FacadeTests.StageWindowsDependency(runtime, output);
            Assert.Equal(longer, File.ReadAllBytes(staged));
        }
        finally { Directory.Delete(directory, true); }
    }
}
