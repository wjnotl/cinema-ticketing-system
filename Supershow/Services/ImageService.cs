using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

namespace Supershow.Services;

public class ImageService
{
	private readonly IWebHostEnvironment en;

	public ImageService(IWebHostEnvironment en)
	{
		this.en = en;
	}

	public string ValidateImage(IFormFile f, int maxSize)
	{
		var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
		var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

		if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
		{
			return "Only JPG and PNG image is allowed.";
		}
		else if (f.Length > maxSize * 1024 * 1024)
		{
			return $"Image size cannot more than {maxSize}MB.";
		}

		return "";
	}

	public string SaveImage(
		IFormFile f, string folder,
		double outputWidth, double outputHeight,
		double previewWidth, double previewHeight,
		double posX, double posY, double scale)
	{
		var file = Guid.NewGuid().ToString("n") + ".jpg";
		var path = Path.Combine(en.WebRootPath, "uploads", folder, file);

		using var stream = f.OpenReadStream();
		using var img = Image.Load(stream);

		// Special crop
		try
		{
			SpecialCrop(img, outputWidth, outputHeight, previewWidth, previewHeight, posX, posY, scale);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}

		img.Save(path);

		return file;
	}

	public void DeleteImage(string file, string folder)
	{
		file = Path.GetFileName(file);
		var path = Path.Combine(en.WebRootPath, "uploads", folder, file);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	/*
    	outputWidth = desired image output width without scaled
    	outputHeight = desired image output height without scaled
		previewWidth = preview image width without scaled
    	previewHeight = preview image height without scaled
    	positionX = x position of the image from center (scaled)
    	positionY = y position of the image from center (scaled)
    	scale = scale of the image
    */
	public void SpecialCrop(Image image,
		double outputWidth, double outputHeight,
		double previewWidth, double previewHeight,
		double positionX, double positionY, double scale
	)
	{
		// Determine aspect ratio
		double currentRatio = (double)image.Height / image.Width;
		double targetRatio = outputHeight / outputWidth;

		int resizedWidth, resizedHeight;
		int previewResizedWidth, previewResizedHeight;
		if (targetRatio > currentRatio)
		{
			// Fit by height
			resizedHeight = (int)Math.Round(outputHeight * scale);
			// Calculate width to preserve aspect ratio
			resizedWidth = (int)Math.Round(resizedHeight / currentRatio);

			previewResizedHeight = (int)Math.Round(previewHeight * scale);
			previewResizedWidth = (int)Math.Round(previewResizedHeight / currentRatio);
		}
		else
		{
			// Fit by width
			resizedWidth = (int)Math.Round(outputWidth * scale);
			// Calculate height to preserve aspect ratio
			resizedHeight = (int)Math.Round(resizedWidth * currentRatio);

			previewResizedWidth = (int)Math.Round(previewWidth * scale);
			previewResizedHeight = (int)Math.Round(previewResizedWidth * currentRatio);
		}

		// Resize image to new dimensions
		image.Mutate(x => x.Resize(new ResizeOptions
		{
			Size = new Size(resizedWidth, resizedHeight),
			Mode = ResizeMode.Stretch
		}));

		double x1 = ((double)previewResizedWidth / 2) - (previewWidth / 2) - positionX;
		double y1 = ((double)previewResizedHeight / 2) - (previewHeight / 2) - positionY;
		x1 *= (double)outputWidth / previewWidth;
		y1 *= (double)outputHeight / previewHeight;

		int cropX = (int)Math.Round(x1);
		int cropY = (int)Math.Round(y1);
		int cropW = (int)Math.Round(outputWidth);
		int cropH = (int)Math.Round(outputHeight);

		if (cropX < 0 || cropY < 0 || cropX + cropW > resizedWidth || cropY + cropH > resizedHeight)
		{
			throw new Exception("The position is out of the image");
		}

		var cropRect = new Rectangle(cropX, cropY, cropW, cropH);
		image.Mutate(x => x.Crop(cropRect));
	}
}