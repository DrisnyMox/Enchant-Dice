
using System.Collections.Generic;
using Photon.Pun;

namespace YG
{
    [System.Serializable]
    public class SavesYG
    {
        // "Технические сохранения" для работы плагина (Не удалять)
        public int idSave;
        public bool isFirstSession = true;
        public string language = "ru";
        public bool promptDone;

        // Тестовые сохранения для демо сцены
        // Можно удалить этот код, но тогда удалите и демо (папка Example)
        public int money = 1;                       // Можно задать полям значения по умолчанию
        public string newPlayerName;
        
        public int countCards;
        public int countStones = 100;
        public bool tutorCompleted;
        public List<User.Dice> deck;
        public List<User.Dice> inventory;
        public int exp;
        public int lvl = 1;
        public int maxWave;
        public bool[] openLevels;

        // Ваши сохранения

        // Кулдауны дневных наград и прогресс рекламных кнопок.
        // Держим рядом с валютой: в PlayerPrefs они сбрасывались сменой браузера
        // или очисткой данных сайта, а начисленные монеты оставались на аккаунте
        public List<RewardEntry> rewardCooldowns = new List<RewardEntry>();
        public bool rewardCooldownsMigrated;

        [System.Serializable]
        public class RewardEntry
        {
            public string key;
            public string value;
        }

        // ...

        // Поля (сохранения) можно удалять и создавать новые. При обновлении игры сохранения ломаться не должны


        // Вы можете выполнить какие то действия при загрузке сохранений
        public SavesYG()
        {
            // Допустим, задать значения по умолчанию для отдельных элементов массива
        }
    }
}
