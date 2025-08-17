using Truman.Data.Entities;

namespace Truman.Api.Features.Articles;

static class ArticlePresenterExtensions
{
    public static ArticlePresenter? ByLabel(this ICollection<ArticlePresenter> articlePresenters, string presenter) =>
        articlePresenters.FirstOrDefault(
            ap => ap.Presenter.Label.Equals(presenter, StringComparison.OrdinalIgnoreCase));
}