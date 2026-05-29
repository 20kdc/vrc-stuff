namespace KDCVRCBSP.ECL {
	/// Locator for a resource.
	public interface IResLocator<T> {
		/// Tries to get the given resource, or returns null.
		public T GetResourceOrNull(string name);
	}
	public static class IResLocatorExtensions {
		/// Tries to get the given resource. Returns def if not found.
		public static T GetResourceOr<T>(this IResLocator<T> locator, string name, T def) {
			T v = locator.GetResourceOrNull(name);
			return v == null ? def : v;
		}
	}
}