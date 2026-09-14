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
}
