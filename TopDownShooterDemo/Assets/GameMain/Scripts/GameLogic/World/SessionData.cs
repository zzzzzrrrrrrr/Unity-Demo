using GameMain.GameLogic.Data;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Runtime-only data for current game session.
    /// </summary>
    public static class SessionData
    {
        public static CharacterData SelectedCharacterData { get; private set; }
        public static Sprite SelectedCharacterSprite { get; private set; }

        public static bool HasSelectedCharacter
        {
            get { return SelectedCharacterData != null; }
        }

        public static void SaveSelectedCharacter(CharacterData selectedCharacter, Sprite selectedCharacterSprite = null)
        {
            SelectedCharacterData = selectedCharacter;
            SelectedCharacterSprite = selectedCharacterSprite;
        }

        public static void Clear()
        {
            SelectedCharacterData = null;
            SelectedCharacterSprite = null;
        }
    }
}
