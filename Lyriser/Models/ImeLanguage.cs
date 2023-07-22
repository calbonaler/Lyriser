namespace Lyriser.Models;

public interface IMonoRubyProvider
{
	Core.Ime.MonoRuby GetMonoRuby(string text);
}

public class ImeLanguage : IMonoRubyProvider
{
	ImeLanguage() { }

	public static readonly IMonoRubyProvider Instance = new ImeLanguage();

	public Core.Ime.MonoRuby GetMonoRuby(string text) { return Core.Ime.Ime.GetMonoRuby(text); }
}
