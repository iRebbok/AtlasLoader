namespace UMF.Patcher
{
	public class Settings
	{
		public string path;
		public string defaultMode;

		public string type;
		public string method;
		public int index;

		public bool Verify() =>
			!string.IsNullOrWhiteSpace(path) &&
			!string.IsNullOrWhiteSpace(defaultMode) &&

			!string.IsNullOrWhiteSpace(type) &&
			!string.IsNullOrWhiteSpace(method) &&
			index >= 0;
	}
}