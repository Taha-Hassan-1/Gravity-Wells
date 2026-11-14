namespace GravityWells.Services
{
    // Sends text out of the game and reads what the player copied. Behind an interface so the Android
    // share sheet, the Editor's clipboard and a test fake are interchangeable.
    public interface IShareService
    {
        // Opens the system share sheet where there is one. False when it could only copy the text
        // to the clipboard instead, so the caller can say so.
        bool Share(string text);

        string Paste();
    }
}
