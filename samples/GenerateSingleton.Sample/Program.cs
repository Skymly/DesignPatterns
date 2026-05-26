using GenerateSingleton.Sample;

Console.WriteLine(AppSettings.Instance.AppName);
Console.WriteLine(FastCache.Instance.GetHashCode());
