using System;
using System.Collections.Generic;
using System.Linq;

namespace TelegramBotConsole_Classes
{
    #region Собственные типы исключений

    public class TaskCountLimitException : Exception
    {
        public TaskCountLimitException(int taskCountLimit)
            : base($"Превышено максимальное количество задач равное {taskCountLimit}") { }
    }

    public class TaskLengthLimitException : Exception
    {
        public TaskLengthLimitException(int taskLength, int taskLengthLimit)
            : base($"Длина задачи '{taskLength}' превышает максимально допустимое значение {taskLengthLimit}") { }
    }

    public class DuplicateTaskException : Exception
    {
        public DuplicateTaskException(string task)
            : base($"Задача '{task}' уже существует") { }
    }

    #endregion

    #region Классы для ООП

    public enum ToDoItemState
    {
        Active,
        Completed
    }

    public class ToDoUser
    {
        public Guid UserId { get; }
        public string TelegramUserName { get; }
        public DateTime RegisteredAtUtc { get; }

        public DateTime RegisteredAtLocal => RegisteredAtUtc.ToLocalTime();

        public ToDoUser(string telegramUserName)
        {
            UserId = Guid.NewGuid();
            TelegramUserName = telegramUserName;
            RegisteredAtUtc = DateTime.UtcNow;
        }
    }

    public class ToDoItem
    {
        public Guid Id { get; }
        public ToDoUser User { get; }
        public string Name { get; }
        public DateTime CreatedAtUtc { get; }
        public ToDoItemState State { get; private set; }
        public DateTime? StateChangedAtUtc { get; private set; }

        public DateTime CreatedAtLocal => CreatedAtUtc.ToLocalTime();
        public DateTime? StateChangedAtLocal => StateChangedAtUtc?.ToLocalTime();

        public ToDoItem(ToDoUser user, string name)
        {
            Id = Guid.NewGuid();
            User = user;
            Name = name;
            CreatedAtUtc = DateTime.UtcNow;
            State = ToDoItemState.Active;
            StateChangedAtUtc = null;
        }

        public void Complete()
        {
            State = ToDoItemState.Completed;
            StateChangedAtUtc = DateTime.UtcNow;
        }
    }

    #endregion

    class Program
    {
        #region Главный метод Main

        static void Main(string[] args)
        {
            try
            {
                Console.Write("Введите максимально допустимое количество задач (1-100): ");
                int maxTasks = ParseAndValidateInt(Console.ReadLine(), 1, 100);

                Console.Write("Введите максимально допустимую длину задачи (1-100): ");
                int maxTaskLength = ParseAndValidateInt(Console.ReadLine(), 1, 100);

                ToDoUser currentUser = null;
                bool isNameEntered = false;
                bool waitingForName = false;
                bool isRunning = true;
                List<ToDoItem> tasks = new List<ToDoItem>();

                PrintWelcomeMessage();

                while (isRunning)
                {
                    if (!waitingForName)
                    {
                        Console.Write("Введите команду: ");
                    }

                    string input = Console.ReadLine();

                    if (waitingForName)
                    {
                        HandleNameInput(input, ref currentUser, ref isNameEntered, ref waitingForName);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine("Пожалуйста, введите команду.\n");
                        continue;
                    }

                    if (input.StartsWith("/echo "))
                    {
                        HandleEchoWithArgument(input, currentUser, isNameEntered);
                        continue;
                    }

                    isRunning = ProcessCommand(input, ref currentUser, ref isNameEntered, ref waitingForName, tasks, maxTasks, maxTaskLength);
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            catch (TaskCountLimitException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            catch (TaskLengthLimitException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            catch (DuplicateTaskException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла непредвиденная ошибка: {ex.Message}");
                Console.WriteLine($"Тип: {ex.GetType()}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Console.WriteLine($"InnerException: {ex.InnerException.Message}");
            }
        }

        #endregion

        #region Вспомогательные методы валидации

        static int ParseAndValidateInt(string? str, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentException($"Ввод не может быть пустым. Ожидается число от {min} до {max}.");

            if (!int.TryParse(str, out int result))
                throw new ArgumentException($"Введено не число. Ожидается целое число от {min} до {max}.");

            if (result < min || result > max)
                throw new ArgumentException($"Число должно быть в диапазоне от {min} до {max}. Вы ввели {result}.");

            return result;
        }

        static void ValidateString(string? str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentException("Строка не может быть пустой или состоять только из пробелов.");
        }

        static void PrintWelcomeMessage()
        {
            Console.WriteLine("\nДобро пожаловать в бот!\n");
            Console.WriteLine("Доступные команды:");
            Console.WriteLine("/start - начать работу и ввести имя");
            Console.WriteLine("/help - показать справку");
            Console.WriteLine("/info - информация о программе");
            Console.WriteLine("/echo [текст] - повторить введенный текст");
            Console.WriteLine("/addtask - добавить задачу");
            Console.WriteLine("/showtasks - показать активные задачи");
            Console.WriteLine("/showalltasks - показать все задачи");
            Console.WriteLine("/completetask [id] - завершить задачу по Id");
            Console.WriteLine("/removetask - удалить задачу по номеру");
            Console.WriteLine("/exit - выход из программы");
            Console.WriteLine();
        }

        #endregion

        #region Обработка ввода имени

        static void HandleNameInput(string input, ref ToDoUser currentUser, ref bool isNameEntered, ref bool waitingForName)
        {
            try
            {
                ValidateString(input);

                if (input.StartsWith("/"))
                {
                    Console.WriteLine($"\nОшибка: '{input}' - это команда, а не имя. Пожалуйста, введите ваше имя (без /):");
                    return;
                }

                if (input.Length < 2)
                {
                    Console.WriteLine("\nОшибка: имя должно содержать хотя бы 2 символа. Попробуйте снова:");
                    return;
                }

                if (!input.All(c => char.IsLetter(c)))
                {
                    Console.WriteLine("\nОшибка: имя должно содержать только буквы. Попробуйте снова:");
                    return;
                }

                currentUser = new ToDoUser(input);
                isNameEntered = true;
                waitingForName = false;
                Console.WriteLine($"\nПриятно познакомиться, {currentUser.TelegramUserName}!");
                Console.WriteLine("Подсказка: теперь доступны все команды!\n");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"\nОшибка: {ex.Message}");
            }
        }

        #endregion

        #region Обработка команды /echo

        static void HandleEchoWithArgument(string input, ToDoUser currentUser, bool isNameEntered)
        {
            if (!isNameEntered)
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
                return;
            }

            string echoText = input.Substring(6);
            if (!string.IsNullOrWhiteSpace(echoText))
            {
                Console.WriteLine($"{currentUser.TelegramUserName}, вы написали: \"{echoText}\"\n");
            }
            else
            {
                Console.WriteLine($"{currentUser.TelegramUserName}, вы не ввели текст. Используйте: /echo текст\n");
            }
        }

        #endregion

        #region Диспетчер команд

        static bool ProcessCommand(string input, ref ToDoUser currentUser, ref bool isNameEntered, ref bool waitingForName, List<ToDoItem> tasks, int maxTasks, int maxTaskLength)
        {
            if (input.StartsWith("/completetask "))
            {
                string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string guidString = parts[1];
                    ExecuteCompleteTaskCommand(isNameEntered, tasks, guidString);
                }
                else
                {
                    Console.WriteLine("Ошибка: укажите Id задачи. Пример: /completetask 583d3e26-0b20-4620-afc0-35a55f24d31d\n");
                }
                return true;
            }

            switch (input)
            {
                case "/start": ExecuteStartCommand(ref waitingForName, isNameEntered, currentUser); 
                    break;
                case "/help": ExecuteHelpCommand(isNameEntered, currentUser); 
                    break;
                case "/info": ExecuteInfoCommand(isNameEntered, currentUser); 
                    break;
                case "/echo": ExecuteEchoCommand(isNameEntered, currentUser); 
                    break;
                case "/addtask": ExecuteAddTaskCommand(isNameEntered, currentUser, tasks, maxTasks, maxTaskLength); 
                    break;
                case "/showtasks": ExecuteShowTasksCommand(isNameEntered, tasks); 
                    break;
                case "/showalltasks": ExecuteShowAllTasksCommand(isNameEntered, tasks); 
                    break;
                case "/removetask": ExecuteRemoveTaskCommand(isNameEntered, tasks); 
                    break;
                case "/exit": ExecuteExitCommand(isNameEntered, currentUser); 
                    return false;
                default: ExecuteUnknownCommand(); 
                    break;
            }
            return true;
        }

        #endregion

        #region Команда /start

        static void ExecuteStartCommand(ref bool waitingForName, bool isNameEntered, ToDoUser currentUser)
        {
            if (!isNameEntered)
            {
                waitingForName = true;
                Console.WriteLine();
                Console.Write("Введите ваше имя: ");
            }
            else
            {
                Console.WriteLine($"{currentUser.TelegramUserName}, вы уже ввели имя. Используйте другие команды.\n");
            }
        }

        #endregion

        #region Команда /help

        static void ExecuteHelpCommand(bool isNameEntered, ToDoUser currentUser)
        {
            Console.WriteLine();
            Console.WriteLine("Справочная информация");
            Console.WriteLine("/start - начать работу и ввести имя");
            Console.WriteLine("/help - показать эту справку");
            Console.WriteLine("/info - информация о программе");
            Console.WriteLine("/echo [текст] - повторить введенный текст");
            Console.WriteLine("/addtask - добавить новую задачу");
            Console.WriteLine("/showtasks - показать активные задачи");
            Console.WriteLine("/showalltasks - показать все задачи (включая завершенные)");
            Console.WriteLine("/completetask [id] - завершить задачу по Id");
            Console.WriteLine("/removetask - удалить задачу по номеру");
            Console.WriteLine("/exit - выход из программы");
            PrintPersonalizedMessage(isNameEntered, currentUser, "это вся доступная информация на данный момент.");
        }

        #endregion

        #region Команда /info

        static void ExecuteInfoCommand(bool isNameEntered, ToDoUser currentUser)
        {
            Console.WriteLine();
            Console.WriteLine("Информация о программе");
            Console.WriteLine("Версия: 5.0.0");
            Console.WriteLine("Дата создания: 21.04.2026");
            Console.WriteLine("Автор: Dorofeeva Daria");
            PrintPersonalizedMessage(isNameEntered, currentUser, "спасибо, что пользуетесь ботом!");
        }

        #endregion

        #region Команда /echo

        static void ExecuteEchoCommand(bool isNameEntered, ToDoUser currentUser)
        {
            Console.WriteLine();
            if (isNameEntered)
            {
                Console.WriteLine($"{currentUser.TelegramUserName}, вы использовали команду /echo без текста. Используйте: /echo [текст]\n");
            }
            else
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
            }
        }

        #endregion

        #region Команда /addtask

        static void ExecuteAddTaskCommand(bool isNameEntered, ToDoUser currentUser, List<ToDoItem> tasks, int maxTasks, int maxTaskLength)
        {
            Console.WriteLine();
            if (!isNameEntered)
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
                return;
            }

            Console.Write("Пожалуйста, введите описание задачи: ");
            string taskDescription = Console.ReadLine();

            try
            {
                ValidateString(taskDescription);

                if (taskDescription.Length > maxTaskLength)
                    throw new TaskLengthLimitException(taskDescription.Length, maxTaskLength);

                if (tasks.Any(t => t.Name == taskDescription))
                    throw new DuplicateTaskException(taskDescription);

                if (tasks.Count >= maxTasks)
                    throw new TaskCountLimitException(maxTasks);

                ToDoItem newTask = new ToDoItem(currentUser, taskDescription);
                tasks.Add(newTask);
                Console.WriteLine($"Задача \"{taskDescription}\" добавлена. Id: {newTask.Id}\n");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}\n");
            }
            catch (TaskLengthLimitException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}\n");
            }
            catch (DuplicateTaskException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}\n");
            }
            catch (TaskCountLimitException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}\n");
            }
        }

        #endregion

        #region Команда /showtasks (только активные)

        static void ExecuteShowTasksCommand(bool isNameEntered, List<ToDoItem> tasks)
        {
            Console.WriteLine();
            if (!isNameEntered)
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
                return;
            }

            var activeTasks = tasks.Where(t => t.State == ToDoItemState.Active).ToList();

            if (activeTasks.Count == 0)
            {
                Console.WriteLine("Список активных задач пуст.\n");
                return;
            }

            Console.WriteLine("Ваши активные задачи:");
            for (int i = 0; i < activeTasks.Count; i++)
            {
                var task = activeTasks[i];
                Console.WriteLine($"{i + 1}. {task.Name} - {task.CreatedAtLocal:dd.MM.yyyy HH:mm:ss} - {task.Id}");
            }
            Console.WriteLine();
        }

        #endregion

        #region Команда /showalltasks

        static void ExecuteShowAllTasksCommand(bool isNameEntered, List<ToDoItem> tasks)
        {
            Console.WriteLine();
            if (!isNameEntered)
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
                return;
            }

            if (tasks.Count == 0)
            {
                Console.WriteLine("Список задач пуст.\n");
                return;
            }

            Console.WriteLine("Все задачи:");
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                string stateIcon = task.State == ToDoItemState.Active ? "Active" : "Completed";
                Console.WriteLine($"{stateIcon} - {task.Name} - {task.CreatedAtLocal:dd.MM.yyyy HH:mm:ss} - {task.Id}");
            }
            Console.WriteLine();
        }

        #endregion

        #region Команда /completetask

        static void ExecuteCompleteTaskCommand(bool isNameEntered, List<ToDoItem> tasks, string guidString)
        {
            Console.WriteLine();
            if (!isNameEntered)
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
                return;
            }

            if (!Guid.TryParse(guidString, out Guid taskId))
            {
                Console.WriteLine($"Ошибка: неверный формат Id. Введите корректный GUID.\n");
                return;
            }

            var task = tasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null)
            {
                Console.WriteLine($"Ошибка: задача с Id '{taskId}' не найдена.\n");
                return;
            }

            if (task.State == ToDoItemState.Completed)
            {
                Console.WriteLine($"Задача \"{task.Name}\" уже завершена.\n");
                return;
            }

            task.Complete();
            Console.WriteLine($"Задача \"{task.Name}\" завершена. Id: {task.Id}\n");
        }

        #endregion

        #region Команда /removetask

        static void ExecuteRemoveTaskCommand(bool isNameEntered, List<ToDoItem> tasks)
        {
            Console.WriteLine();
            if (!isNameEntered)
            {
                Console.WriteLine("Сначала введите /start, чтобы начать работу.\n");
                return;
            }

            if (tasks.Count == 0)
            {
                Console.WriteLine("Список задач пуст. Нечего удалять.\n");
                return;
            }

            Console.WriteLine("Вот ваш список задач:");
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                string stateIcon = task.State == ToDoItemState.Active ? "🟢" : "✅";
                Console.WriteLine($"{i + 1}. {stateIcon} {task.Name} - {task.CreatedAtLocal:dd.MM.yyyy HH:mm:ss}");
            }

            Console.Write("Введите номер задачи для удаления: ");
            string numberInput = Console.ReadLine();

            if (int.TryParse(numberInput, out int taskNumber))
            {
                if (taskNumber >= 1 && taskNumber <= tasks.Count)
                {
                    ToDoItem removedTask = tasks[taskNumber - 1];
                    tasks.RemoveAt(taskNumber - 1);
                    Console.WriteLine($"Задача \"{removedTask.Name}\" удалена.\n");
                }
                else
                {
                    Console.WriteLine($"Ошибка: введите номер от 1 до {tasks.Count}.\n");
                }
            }
            else
            {
                Console.WriteLine("Ошибка: введите корректный номер задачи.\n");
            }
        }

        #endregion

        #region Команда /exit

        static void ExecuteExitCommand(bool isNameEntered, ToDoUser currentUser)
        {
            Console.WriteLine();
            if (isNameEntered)
                Console.WriteLine($"До свидания, {currentUser.TelegramUserName}!");
            else
                Console.WriteLine("До свидания!");

            Console.WriteLine("Программа завершена.");
        }

        #endregion

        #region Неизвестная команда

        static void ExecuteUnknownCommand()
        {
            Console.WriteLine("Неизвестная команда. Введите /help для списка команд.\n");
        }

        #endregion

        #region Вспомогательный метод

        static void PrintPersonalizedMessage(bool isNameEntered, ToDoUser currentUser, string message)
        {
            if (isNameEntered)
                Console.WriteLine($"{currentUser.TelegramUserName}, {message}\n");
            else
                Console.WriteLine($"Совет: сначала введите /start\n");
        }

        #endregion
    }
}