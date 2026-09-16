using System;
using System.IO;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestDefinitionCollectionTests
{
    private const string ValidQuest =
        "{\"QuestId\":1,\"GiverGuid\":100,\"TargetGuid\":200," +
        "\"Goals\":[{\"NameId\":10,\"Type\":0,\"TargetGuid\":200}]}";

    private static bool Load(string json, out QuestDefinitionCollection quests)
    {
        var path = Path.Combine(Path.GetTempPath(), $"quests-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);

        try
        {
            quests = new QuestDefinitionCollection(NullLogger.Instance);
            return quests.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertRejected(string json)
    {
        Assert.IsFalse(Load(json, out var quests));
        Assert.AreEqual(0, quests.Count);
    }

    [TestMethod]
    public void Load_AcceptsAValidQuest()
    {
        Assert.IsTrue(Load($"[{ValidQuest}]", out var quests));
        Assert.AreEqual(1, quests.Count);
    }

    [TestMethod]
    public void Load_MissingFileLeavesTheServerWithoutQuests()
    {
        var quests = new QuestDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(quests.Load(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json")));
        Assert.AreEqual(0, quests.Count);
    }

    [TestMethod]
    public void Load_IndexesGiversAndTargets()
    {
        Assert.IsTrue(Load($"[{ValidQuest}]", out var quests));

        Assert.AreEqual(1, quests.QuestsOfferedBy(100).Count);
        Assert.AreEqual(1, quests.QuestsOfferedBy(100)[0]);

        Assert.AreEqual(1, quests.QuestsTargeting(200).Count);
        Assert.AreEqual(1, quests.QuestsTargeting(200)[0]);

        Assert.IsTrue(quests.IsQuestNpc(100));
        Assert.IsTrue(quests.IsQuestNpc(200));
        Assert.IsFalse(quests.IsQuestNpc(999));
        Assert.AreEqual(0, quests.QuestsOfferedBy(999).Count);
    }

    [TestMethod]
    public void Load_RejectsDuplicateQuestIds()
    {
        AssertRejected($"[{ValidQuest},{ValidQuest}]");
    }

    [TestMethod]
    public void Load_RejectsAQuestWithNoGiver()
    {
        AssertRejected("[{\"QuestId\":1,\"Goals\":[{\"NameId\":10,\"TargetGuid\":200}]}]");
    }

    [TestMethod]
    public void Load_RejectsAQuestWithNoGoals()
    {
        AssertRejected("[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[]}]");
    }

    [TestMethod]
    public void Load_RejectsGoalsThatShareANameId()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"TargetGuid\":200,\"Goals\":[" +
            "{\"NameId\":10,\"TargetGuid\":200},{\"NameId\":10,\"TargetGuid\":201}]}]");
    }

    [TestMethod]
    public void Load_RejectsAGoalWithoutANameId()
    {
        AssertRejected("[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[{\"TargetGuid\":200}]}]");
    }

    [TestMethod]
    public void Load_RejectsATalkGoalWithNobodyToTalkTo()
    {
        AssertRejected("[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[{\"NameId\":10,\"Type\":0}]}]");
    }

    [TestMethod]
    public void Load_RejectsATravelGoalWithoutAPosition()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[" +
            "{\"NameId\":10,\"Type\":1,\"ReachPosition\":[1.0,2.0]}]}]");
    }

    [TestMethod]
    public void Load_RejectsAGatherGoalWithoutANodeType()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[" +
            "{\"NameId\":10,\"Type\":2,\"RequiredCount\":3}]}]");
    }

    [TestMethod]
    public void Load_RejectsAGatherGoalWithoutARequiredCount()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[" +
            "{\"NameId\":10,\"Type\":2,\"CollectNodeType\":\"tin-ore\"}]}]");
    }

    [TestMethod]
    public void Load_RejectsAnUnknownPrerequisite()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"TargetGuid\":200,\"PrerequisiteQuestId\":42," +
            "\"Goals\":[{\"NameId\":10,\"TargetGuid\":200}]}]");
    }

    [TestMethod]
    public void Load_RejectsAnUnknownFollowOnQuest()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"TargetGuid\":200,\"NextQuestId\":42," +
            "\"Goals\":[{\"NameId\":10,\"TargetGuid\":200}]}]");
    }

    [TestMethod]
    public void Load_RejectsAnUnknownExclusion()
    {
        AssertRejected(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"TargetGuid\":200,\"ExcludesQuestIds\":[42]," +
            "\"Goals\":[{\"NameId\":10,\"TargetGuid\":200}]}]");
    }

    [TestMethod]
    public void Load_AcceptsQuestsThatReferenceEachOther()
    {
        Assert.IsTrue(Load(
            "[{\"QuestId\":1,\"GiverGuid\":100,\"TargetGuid\":200,\"NextQuestId\":2," +
            "\"Goals\":[{\"NameId\":10,\"TargetGuid\":200}]}," +
            "{\"QuestId\":2,\"GiverGuid\":100,\"TargetGuid\":200,\"PrerequisiteQuestId\":1," +
            "\"ExcludesQuestIds\":[1],\"Goals\":[{\"NameId\":11,\"TargetGuid\":200}]}]", out var quests));

        Assert.AreEqual(2, quests.Count);
    }

    [TestMethod]
    public void Load_RejectsMalformedJson()
    {
        AssertRejected("[{\"QuestId\":1,");
    }

    [TestMethod]
    public void Load_LeavesPreviousQuestsInPlaceWhenAReloadFails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quests-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, $"[{ValidQuest}]");

            var quests = new QuestDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(quests.Load(path));
            Assert.AreEqual(1, quests.Count);

            File.WriteAllText(path, "[{\"QuestId\":1,\"GiverGuid\":100,\"Goals\":[]}]");

            Assert.IsFalse(quests.Load(path));
            Assert.AreEqual(1, quests.Count);
            Assert.IsTrue(quests.IsQuestNpc(100));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
