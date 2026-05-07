namespace TestCases.NestedFromFormBindingTest;

class Request
{
    [FromForm]
    public required FormData Data { get; set; }

    public class FormData
    {
        public required string Name { get; set; }
        [BindFrom("custom_name")]
        public required string CustomName { get; set; }
        public required IFormFile File { get; set; }
        public required IFormFileCollection Documents { get; set; }
        public required Details Details { get; set; }
        public required List<Item> Items { get; set; }
        public required List<int> Numbers { get; set; }
    }

    public class Details
    {
        public required string Title { get; set; }
        public required IFormFile Image { get; set; }
        public required List<IFormFile> Gallery { get; set; }
    }

    public class Item
    {
        public required string Description { get; set; }
        public required IFormFile Attachment { get; set; }
    }
}

sealed class Response
{
    public string Name { get; set; }
    public string CustomName { get; set; }
    public string FileName { get; set; }
    public string[] DocumentFileNames { get; set; }
    public string DetailsTitle { get; set; }
    public string DetailsImageFileName { get; set; }
    public string[] GalleryFileNames { get; set; }
    public string FirstItemDescription { get; set; }
    public string FirstItemAttachmentFileName { get; set; }
    public int NumbersSum { get; set; }
}

sealed class CircularRequest
{
    [FromForm]
    public required CircularFormData Data { get; set; }
}

sealed class CircularFormData
{
    public string? Name { get; set; }
    public CircularFormData? Child { get; set; }
}

sealed class CircularResponse
{
    public string? Name { get; set; }
    public string? ChildName { get; set; }
}
