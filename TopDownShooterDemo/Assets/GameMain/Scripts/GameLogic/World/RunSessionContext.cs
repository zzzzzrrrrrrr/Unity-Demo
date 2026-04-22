using GameMain.GameLogic.Data;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Alias context used by runtime scenes to query current selected character.
    /// </summary>
    public static class RunSessionContext
    {
        public static CharacterData SelectedCharacterData
        {
            get { return SessionData.SelectedCharacterData; }
        }

        public static bool HasSelectedCharacter
        {
            get { return SessionData.HasSelectedCharacter; }
        }

        public static Sprite SelectedCharacterSprite
        {
            get { return SessionData.SelectedCharacterSprite; }
        }

        public static void SetSelectedCharacter(CharacterData selectedCharacter)
        {
            SessionData.SaveSelectedCharacter(selectedCharacter);
        }

        public static void SetSelectedCharacter(CharacterData selectedCharacter, Sprite selectedCharacterSprite)
        {
            SessionData.SaveSelectedCharacter(selectedCharacter, selectedCharacterSprite);
        }

        public static void Clear()
        {
            SessionData.Clear();
        }
    }
}
