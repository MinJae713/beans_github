using Colosseum.Card;
using Colosseum.UI;
using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class CardSelectionVisualAssemblyTests
    {
        [Test]
        public void VisualAssembly_UsesCardIconAsInternalIllustration()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            var card = ScriptableObject.CreateInstance<CardData>();
            card.rarity = CardRarity.Epic;
            card.cardIcon = sprite;

            CardSelectionUI.CardVisualAssembly visual = CardSelectionUI.ResolveCardVisualAssembly(card);

            Assert.That(visual.HasCard, Is.True);
            Assert.That(visual.Rarity, Is.EqualTo(CardRarity.Epic));
            Assert.That(visual.FrameVariantIndex, Is.EqualTo(CardSelectionUI.GetFrameVariantIndex(CardRarity.Epic)));
            Assert.That(visual.Illustration, Is.SameAs(sprite));
            Assert.That(visual.HasIllustration, Is.True);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void FrameVariantIndex_MapsFourRaritiesToStableSlots()
        {
            Assert.That(CardSelectionUI.GetFrameVariantIndex(CardRarity.Common), Is.EqualTo(0));
            Assert.That(CardSelectionUI.GetFrameVariantIndex(CardRarity.Rare), Is.EqualTo(1));
            Assert.That(CardSelectionUI.GetFrameVariantIndex(CardRarity.Epic), Is.EqualTo(2));
            Assert.That(CardSelectionUI.GetFrameVariantIndex(CardRarity.Legendary), Is.EqualTo(3));
        }

        [Test]
        public void VisualAssembly_NullCardKeepsOfflineFrameAndNoIllustration()
        {
            CardSelectionUI.CardVisualAssembly visual = CardSelectionUI.ResolveCardVisualAssembly(null);

            Assert.That(visual.HasCard, Is.False);
            Assert.That(visual.FrameVariantIndex, Is.EqualTo(-1));
            Assert.That(visual.Illustration, Is.Null);
            Assert.That(visual.HasIllustration, Is.False);
        }

        [Test]
        public void CardSelectionLayout_UsesStraightCardsAndFrameAlignedTextBoxes()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Presentation",
                "UI",
                "CardSelectionUI.cs"));

            Assert.That(source, Does.Contain("public float[] cardAngles = { 0f, 0f, 0f };"));
            Assert.That(source, Does.Contain("return 0f;"));
            Assert.That(source, Does.Contain("RarityBox"));
            Assert.That(source, Does.Contain("CardNameBox"));
            Assert.That(source, Does.Contain("CardDescriptionBox"));
            Assert.That(source, Does.Contain("TransparentTextBoxColor"));
            Assert.That(source, Does.Contain("CardTextColor = Color.white"));
            Assert.That(source, Does.Contain("ConfigureBoxRect"));
            Assert.That(source, Does.Contain("TextOverflowModes.Ellipsis"));
            Assert.That(source, Does.Contain("new Vector2(0.155f, 0.145f), new Vector2(0.845f, 0.295f)"));
        }
    }
}
