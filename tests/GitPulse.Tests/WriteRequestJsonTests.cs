using System.Text.Json;
using GitPulse.Core.Models;
using Xunit;

namespace GitPulse.Tests;

public class WriteRequestJsonTests
{
    [Fact]
    public void IssueUpdateRequest_CloseOnly_OmitsUnsetMembers()
    {
        var json = JsonSerializer.Serialize(new IssueUpdateRequest { State = "closed" });

        Assert.Equal("""{"state":"closed"}""", json);
        Assert.DoesNotContain("title", json, StringComparison.Ordinal);
        Assert.DoesNotContain("body", json, StringComparison.Ordinal);
        Assert.DoesNotContain("labels", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IssueUpdateRequest_TitleBodySave_OmitsStateAndLabels()
    {
        var json = JsonSerializer.Serialize(new IssueUpdateRequest
        {
            Title = "Keep the title",
            Body = "Keep the body",
        });

        Assert.Equal("""{"title":"Keep the title","body":"Keep the body"}""", json);
        Assert.DoesNotContain("state", json, StringComparison.Ordinal);
        Assert.DoesNotContain("labels", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_UnsetLine_OmitsLineMember()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "file comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_FileSubject_OmitsLineAndWritesSubjectType()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "file comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
                SubjectType = "file",
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
        Assert.Contains("\"subject_type\":\"file\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_WithLine_WritesLineAndOmitsSubjectType()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "line comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
                Line = 12,
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"line\":12", json, StringComparison.Ordinal);
        Assert.DoesNotContain("subject_type", json, StringComparison.Ordinal);
    }
}
