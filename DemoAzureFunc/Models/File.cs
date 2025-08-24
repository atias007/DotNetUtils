using System.ComponentModel.DataAnnotations.Schema;

namespace DemoAzureFunc.Models;

[Table("Files")]
internal class File
{
    public required string Filename { get; set; }
    public required string Content { get; set; }
    public required int Chunk { get; set; }
}