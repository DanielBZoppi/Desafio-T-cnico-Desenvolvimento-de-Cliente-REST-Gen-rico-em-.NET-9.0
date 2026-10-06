namespace SampleApp;


public record Post(int UserId, int Id, string Title, string Body);

public record NewPost(string Title, string Body, int UserId);
