namespace Game.Client.Tutorial
{
    public static class TutorialEntryRoute
    {
        public const string SceneName = "Tutorial";

        public static string Resolve(string configuredNextScene, ITutorialCompletionStore store)
        {
            if (store == null || store.IsCurrentVersionCompleted)
            {
                return configuredNextScene;
            }

            return SceneName;
        }
    }
}
