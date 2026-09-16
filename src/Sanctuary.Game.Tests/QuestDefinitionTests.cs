using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestDefinitionTests
{
    private static QuestDefinition Quest(int questId = 1) => new()
    {
        QuestId = questId,
        GiverGuid = 100,
        Goals = [new QuestGoal { NameId = 10, TargetGuid = 200 }]
    };

    [TestMethod]
    public void IsOfferableFor_QuestAlreadyTaken_IsNotOffered()
    {
        var quest = Quest();

        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool> { [1] = false }));
        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool> { [1] = true }));
    }

    [TestMethod]
    public void IsOfferableFor_NothingTaken_IsOffered()
    {
        Assert.IsTrue(Quest().IsOfferableFor(new Dictionary<int, bool>()));
    }

    [TestMethod]
    public void IsOfferableFor_ExcludedQuestTaken_IsNotOffered()
    {
        var quest = Quest();
        quest.ExcludesQuestIds = [2];

        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool> { [2] = false }));
    }

    /// <summary>
    /// The branching starter-job choice needs both: a quest unlocked by finishing another one, which
    /// is still ruled out once the player has taken the other branch.
    /// </summary>
    [TestMethod]
    public void IsOfferableFor_ExcludedQuestTaken_IsNotOfferedEvenWhenThePrerequisiteIsMet()
    {
        var quest = Quest();
        quest.PrerequisiteQuestId = 3;
        quest.ExcludesQuestIds = [2];

        var playerQuests = new Dictionary<int, bool> { [3] = true, [2] = false };

        Assert.IsFalse(quest.IsOfferableFor(playerQuests));
    }

    [TestMethod]
    public void IsOfferableFor_PrerequisiteAcceptedButNotCompleted_IsNotOffered()
    {
        var quest = Quest();
        quest.PrerequisiteQuestId = 3;

        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool> { [3] = false }));
    }

    [TestMethod]
    public void IsOfferableFor_PrerequisiteCompleted_IsOffered()
    {
        var quest = Quest();
        quest.PrerequisiteQuestId = 3;

        Assert.IsTrue(quest.IsOfferableFor(new Dictionary<int, bool> { [3] = true }));
    }

    [TestMethod]
    public void TurnInDialogueId_ComesFromTheLastGoal()
    {
        var quest = Quest();
        quest.Goals =
        [
            new QuestGoal { NameId = 10, DialogueId = 111 },
            new QuestGoal { NameId = 11, DialogueId = 222 }
        ];

        Assert.AreEqual(222, quest.TurnInDialogueId);
    }

    [TestMethod]
    public void AllTalkTargetGuids_ListsEachNpcOnce()
    {
        var goal = new QuestGoal { NameId = 10, TargetGuid = 200, TargetGuids = [200, 201, 0] };

        CollectionAssert.AreEqual(new ulong[] { 200, 201 }, goal.AllTalkTargetGuids().ToArray());
    }

    [TestMethod]
    public void ConversationFor_PrefersTheSharedConversation()
    {
        var goal = new QuestGoal
        {
            NameId = 10,
            TargetGuid = 200,
            DialogueId = 999,
            Dialogue = [new QuestDialogueLine { TextId = 1 }, new QuestDialogueLine { TextId = 2 }]
        };

        var lines = goal.ConversationFor(200);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual(1, lines[0].TextId);
    }

    [TestMethod]
    public void ConversationFor_UsesTheLinePairedWithThatNpc()
    {
        var goal = new QuestGoal
        {
            NameId = 10,
            TargetGuid = 200,
            TargetGuids = [201],
            TargetDialogueIds = [11, 22],
            TargetResponseIds = [33, 44]
        };

        var lines = goal.ConversationFor(201);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(22, lines[0].TextId);
        Assert.AreEqual(44, lines[0].ResponseTextId);
    }

    [TestMethod]
    public void ConversationFor_FallsBackToTheGoalDialogue()
    {
        var goal = new QuestGoal { NameId = 10, TargetGuid = 200, DialogueId = 777 };

        var lines = goal.ConversationFor(200);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(777, lines[0].TextId);
    }

    [TestMethod]
    public void ConversationFor_SaysNothingWhenNoDialogueIsAuthored()
    {
        var goal = new QuestGoal { NameId = 10, TargetGuid = 200 };

        Assert.AreEqual(0, goal.ConversationFor(200).Count);
    }

    [TestMethod]
    public void IsCountedTalk_OnlyWhenATalkGoalNeedsMoreThanOneNpc()
    {
        Assert.IsFalse(new QuestGoal { RequiredCount = 1 }.IsCountedTalk);
        Assert.IsTrue(new QuestGoal { RequiredCount = 3 }.IsCountedTalk);
        Assert.IsFalse(new QuestGoal { Type = QuestGoalType.Collect, RequiredCount = 3 }.IsCountedTalk);
    }
}
