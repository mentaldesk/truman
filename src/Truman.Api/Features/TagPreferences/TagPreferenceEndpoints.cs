using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Truman.Api.Features.TagPreferences;

namespace Truman.Api.Features.TagPreferences;

public static class TagPreferenceEndpoints
{
    public static void MapTagPreferenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tag-preferences")
            .WithTags("Tag Preferences")
            .RequireAuthorization();

        // Get user's tag preferences
        group.MapGet("/", async (
            ITagPreferenceService tagPreferenceService,
            HttpContext httpContext) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

            var preferences = await tagPreferenceService.GetUserTagPreferencesAsync(email);
            return Results.Ok(preferences);
        });

        // Set a tag preference (add or update)
        group.MapPost("/", async (
            [FromBody] SetTagPreferenceRequest request,
            ITagPreferenceService tagPreferenceService,
            HttpContext httpContext) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Tag))
            {
                return Results.BadRequest("Tag is required");
            }

            if (request.Weight < 0)
            {
                return Results.BadRequest("Weight must be 0 (banned) or 1+ (favorite)");
            }

            try
            {
                var result = await tagPreferenceService.SetTagPreferenceAsync(email, request.Tag, request.Weight);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(ex.Message);
            }
        });

        // Remove a tag preference
        group.MapDelete("/{tag}", async (
            string tag,
            ITagPreferenceService tagPreferenceService,
            HttpContext httpContext) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(tag))
            {
                return Results.BadRequest("Tag is required");
            }

            var removed = await tagPreferenceService.RemoveTagPreferenceAsync(email, tag);
            
            if (removed)
            {
                return Results.Ok(new { message = $"Tag preference '{tag}' removed successfully" });
            }
            else
            {
                return Results.NotFound($"Tag preference '{tag}' not found for user");
            }
        });

        // Promote tag priority (move to next weight group)
        group.MapPost("/{tag}/promote", async (
            string tag,
            ITagPreferenceService tagPreferenceService,
            HttpContext httpContext) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(tag))
            {
                return Results.BadRequest("Tag is required");
            }

            try
            {
                var result = await tagPreferenceService.PromoteTagAsync(email, tag);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // Demote tag priority (move to previous weight group)
        group.MapPost("/{tag}/demote", async (
            string tag,
            ITagPreferenceService tagPreferenceService,
            HttpContext httpContext) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(tag))
            {
                return Results.BadRequest("Tag is required");
            }

            try
            {
                var result = await tagPreferenceService.DemoteTagAsync(email, tag);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // Get tags grouped by weight
        group.MapGet("/groups", async (
            ITagPreferenceService tagPreferenceService,
            HttpContext httpContext) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

            var groups = await tagPreferenceService.GetTagsByWeightGroupAsync(email);
            return Results.Ok(groups);
        });
    }
}
