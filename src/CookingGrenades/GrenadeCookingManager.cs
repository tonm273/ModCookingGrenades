namespace CookingGrenades;

public static class GrenadeCookingManager
{
	private static GrenadeCookingTimer _timer = new GrenadeCookingTimer();

	public static GrenadeCookingTimer Timer => _timer;

	public static GrenadeCookingTimer GetCookingTimer()
	{
		return _timer;
	}
}