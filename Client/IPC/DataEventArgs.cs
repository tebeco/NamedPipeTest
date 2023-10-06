namespace Client.IPC;

public class DataEventArgs : EventArgs
{
    public string Title { get; set; }
    public string Message { get; set; }

    public DataEventArgs()
    {
    }

    public DataEventArgs(string data)
    {
        string[] parts = data.Split(',');
        if (parts.Length == 2)
        {
            this.Title = parts[0];
            this.Message = parts[1];
        }
        else
        {
            this.Title = "N/A";
            this.Message = parts[0];
        }
    }
}