using FluentValidation;

namespace Uploads.Image.SaveTyped;

public class Request
{
    public string ID { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public IFormFile File1 { get; set; }
    public IFormFile File2 { get; set; }
}

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(i => i.Width)
            .GreaterThan(10).WithMessage("Image width too small")
            .LessThan(2000).WithMessage("Image width is too large");

        RuleFor(i => i.Height)
            .GreaterThan(10).WithMessage("Image height too small")
            .LessThan(2000).WithMessage("Image width is too large");
    }
}
