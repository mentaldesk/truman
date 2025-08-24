using Truman.Api.Features.Articles;
using Truman.Data.Entities;

namespace Truman.Api.Tests.Articles;

public class ArticlePresenterExtensionsTests
{
    private static ArticlePresenter Make(string label) => new()
    {
        Presenter = new Presenter { Label = label, PresenterStyle = label + "Style" },
        Title = label + " Title",
        Tldr = label + " TLDR",
        Content = label + " Content"
    };

    [Fact]
    public void ByLabel_FindsPresenter_CaseInsensitive()
    {
        var list = new List<ArticlePresenter> { Make("Default"), Make("Brief") };
        var found = list.ByLabel("brief");
        Assert.NotNull(found);
        Assert.Equal("Brief Title", found!.Title);
    }

    [Fact]
    public void ByLabel_ReturnsNull_WhenMissing()
    {
        var list = new List<ArticlePresenter> { Make("Default") };
        var found = list.ByLabel("Other");
        Assert.Null(found);
    }

    [Fact]
    public void ByLabel_ReturnsNull_OnEmpty()
    {
        var list = new List<ArticlePresenter>();
        Assert.Null(list.ByLabel("Anything"));
    }
}

