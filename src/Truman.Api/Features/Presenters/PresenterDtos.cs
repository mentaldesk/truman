namespace Truman.Api.Features.Presenters;

public record PresenterDto(int Id, string Label, string PresenterStyle);

public record PresenterOptionDto(int Id, string Label);

public record CreatePresenterRequest(string Label, string PresenterStyle);

public record UpdatePresenterRequest(string? Label, string? PresenterStyle);
