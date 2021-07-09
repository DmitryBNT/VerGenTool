using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VerGenTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /* Расчёт контрольной цифры.
         * 1. Рассчитываются произведения значений разрядов расчётного счёта на соответствующие весовые коэффициенты.
         * 2. Рассчитывается остаток деления суммы произведений на 10.
         * 3. Полученная сумма умножается на 3.
         * 4. Значение контрольного ключа (К) принимается равным младшему разряду полученного произведения (остаток деления произведения на 10).
         */
        public static int CheckKey(string account, int[] BankBICDigest)
        {
            // Паттерн весовых коэффициентов.
            int[] WeightingFactorsPattern = { 7, 1, 3 };

            // Заполнение массива весовыми коэффициентами: (7,1,3,7,1,3,7,1,3,7,1,3,7,1,3,7,1,3,7,1,3,7,1).
            int[] WeightingFactors = new int[20];
            int WeightingFactorsIndex = 0, PatternIndex = 0;
            while (WeightingFactorsIndex < 20)
            {
                WeightingFactors[WeightingFactorsIndex] = WeightingFactorsPattern[PatternIndex];
                WeightingFactorsIndex++;
                PatternIndex = PatternIndex++ % 3;
            }

            // Расчёт контрольной суммы.
            List<int> CheckSum = new();

            // Часть I. Расчёт контрольной суммы для трёх последних цифр БИКа.
            int CurrentIndex = 0;
            while (CurrentIndex < 3)
            {
                CheckSum.Add(BankBICDigest[CurrentIndex] * WeightingFactorsPattern[CurrentIndex]);
                CurrentIndex += 1;
            }

            // Часть II. Расчёт контрольной суммы для расчётного счёта.
            CurrentIndex = 0;
            while (CurrentIndex < 20)
            {
                CheckSum.Add(account[CurrentIndex] * WeightingFactors[CurrentIndex]);
                CurrentIndex += 1;
            }

            // Вычисление контрольного числа.
            int CardCheckNumber = 0;
            foreach (int Element in CheckSum)
            {
                CardCheckNumber += Element;
            }

            return CardCheckNumber % 10 * 3 % 10;
        }

        /* Корректировка расчётного счёта.
         * 1. Контрольный разряд приравнивается нулю.
         * 2. Вызывается функция расчёта контрольной цифры.
         */
        public void SettlementAccountСorrection()
        {
            /* Определённые цифры БИКа банка.
             * Для кредитных организаций - три последние цифры БИКа банка.
             * Для расчетно-кассовых центров - "0" и два знака БИКа банка, начиная с пятого знака (разряды 5 и 6).
             */
            int[] BankBICDigest;

            // Получение БИКа банка.
            string BankBIC = SettlementAccountСorrectionBIC.Text;

            // Обнуление контрольного числа в расчетном счёте.
            string First8SettlementAccountDigest = new(SettlementAccountСorrectionAccount.Text.ToCharArray().
                Take(8).ToArray());
            string Last11SettlementAccountDigest = new(SettlementAccountСorrectionAccount.Text.ToCharArray().Skip(9)
                .Take(11).ToArray());
            string SettlementAccount = First8SettlementAccountDigest + "0" + Last11SettlementAccountDigest;

            if (BankBIC.ToCharArray().Length < 9)
            {
                ShowErrorMessage(1);
                return;
            }
            if (SettlementAccount.ToCharArray().Length < 20)
            {
                ShowErrorMessage(4);
                return;
            }

            if (KeyingCB.IsChecked == false)
            {
                // Расчётный счёт открыт в кредитной организации. Алгоритм ключевания расчётного счёта.
                BankBICDigest = BankBIC.ToCharArray().Skip(6)
                    .Take(3).Select(c => (int)char.GetNumericValue(c)).ToArray();
            }
            else
            {
                // Расчётный счёт открыт в РКЦ. Алгоритм ключевания корреспондентского счета.
                BankBICDigest = new int[1];
                BankBICDigest[0] = 0;
                BankBICDigest = BankBICDigest.Concat(BankBIC.ToCharArray().Skip(4)
                    .Take(2).Select(c => (int)char.GetNumericValue(c)).ToArray()).ToArray();
            }

            // Получение контрольной цифры.
            int CardCheckNumber = CheckKey(SettlementAccount, BankBICDigest);

            string First8ResetSettlementAccountDigest = string.Join("", SettlementAccount.ToCharArray()
                .Take(8).Select(c => (int)char.GetNumericValue(c)).ToArray());
            string Last11ResetSettlementAccountDigest = string.Join("", SettlementAccount.ToCharArray().Skip(9)
                .Take(11).Select(c => (int)char.GetNumericValue(c)).ToArray());
            string CorrectSettlementAccount = First8ResetSettlementAccountDigest + CardCheckNumber.ToString() + Last11ResetSettlementAccountDigest;

            SettlementAccountСorrectionKey.Text = CardCheckNumber.ToString();

            SettlementAccountСorrectionCorrectAccount.Text = CorrectSettlementAccount;
        }

        /*
         * Проверка расчётного счёта, открытого в кредитной организации:
         * 1. Для проверки контрольной суммы перед расчётным счётом добавляются три последние цифры БИК банка (итого 23 знака).
         * 2. Вычисляется контрольная сумма со следующими весовыми коэффициентами: (7,1,3,7,1,3,7,1,3,7,1,3,7,1,3,7,1,3,7,1,3,7,1).
         * 3. Вычисляется контрольное число как остаток от деления контрольной суммы на 10.
         * 4. Контрольное число сравнивается с нулём.
         * В случае их равенства расчётный счёт считается правильным.
         */
        public void GenerateSettlementAccount()
        {
            /* Определённые цифры БИКа банка.
             * Для кредитных организаций - три последние цифры БИКа банка.
             * Для расчетно-кассовых центров - "0" и два знака БИКа банка, начиная с пятого знака (разряды 5 и 6).
             */
            int[] BankBICDigest;

            // Получение БИКа банка.
            string BankBIC = GenerateSettlementAccountBIC.Text;

            // Получение кода валюты счёта.
            string Currency = GenerateSettlementAccountCurrency.Text;

            // Получение балансового счёта.
            string BalanceSheet = GenerateSettlementAccountBalanceSheet.Text;

            if (BankBIC.ToCharArray().Length < 9)
            {
                ShowErrorMessage(1);
                return;
            }
            if (Currency.ToCharArray().Length < 3)
            {
                ShowErrorMessage(2);
                return;
            }
            if (BalanceSheet.ToCharArray().Length < 5)
            {
                ShowErrorMessage(3);
                return;
            }

            if (GenerateAccountCB.IsChecked == false)
            {
                // Расчётный счёт открыт в кредитной организации. Алгоритм ключевания расчётного счёта.
                BankBICDigest = BankBIC.ToCharArray().Skip(6)
                    .Take(3).Select(c => (int)char.GetNumericValue(c)).ToArray();
            }
            else
            {
                // Расчётный счёт открыт в РКЦ. Алгоритм ключевания корреспондентского счета.
                BankBICDigest = new int[1];
                BankBICDigest[0] = 0;
                BankBICDigest = BankBICDigest.Concat(BankBIC.ToCharArray().Skip(4)
                    .Take(2).Select(c => (int)char.GetNumericValue(c)).ToArray()).ToArray();
            }

            // Часть I. Формирование первой половины расчётного счёта: балансовый счёт, код валюты счёта и ключ (контрольная цифра).
            string SettlementAccount = BalanceSheet + Currency + "0";

            // Часть II. Формирование второй половины расчётного счёта: номер (порядковый) счёта.
            // Генерация количества ненулевых позиций.
            int NumberNonZeroPositions = new Random().Next(12);
            // Формирование номера (порядкового) счёта.
            int[] AccountNumber = new int[NumberNonZeroPositions];
            for (int i = 0; i < NumberNonZeroPositions; i++)
            {
                AccountNumber[i] = new Random().Next(10);
            }
            // Если получившийся номер меньше 11 позиций, то номер дозаполняется нулями.
            if (AccountNumber.Length < 11)
            {
                int CurrentIndex = 0;
                int[] fill = new int[11 - AccountNumber.Length];
                while (CurrentIndex < fill.Length)
                {
                    fill[CurrentIndex] = 0;
                    CurrentIndex++;
                }
                SettlementAccount = SettlementAccount + string.Join("", fill) + string.Join("", AccountNumber);
            }
            else
            {
                SettlementAccount += string.Join("", AccountNumber);
            }

            // Получение контрольной цифры.
            int CardCheckNumber = CheckKey(SettlementAccount, BankBICDigest);

            string First8ResetSettlementAccountDigest = string.Join("", SettlementAccount.ToCharArray()
                .Take(8).Select(c => (int)char.GetNumericValue(c)).ToArray());
            string Last11ResetSettlementAccountDigest = string.Join("", SettlementAccount.ToCharArray().Skip(9)
                .Take(11).Select(c => (int)char.GetNumericValue(c)).ToArray());
            string CorrectSettlementAccount = First8ResetSettlementAccountDigest + CardCheckNumber.ToString() + Last11ResetSettlementAccountDigest;


            GenerateSettlementAccountCorrectAccount.Text = CorrectSettlementAccount;
        }

        private void ShowErrorMessage(int Code)
        {
            string ErrorMessage;
            switch (Code)
            {
                case 1:
                    ErrorMessage = "В поле \"БИК банка\" меньше 9 символов.";
                    break;
                case 2:
                    ErrorMessage = "В поле \"Код валюты\" меньше 3 символов.";
                    break;
                case 3:
                    ErrorMessage = "В поле \"Балансовый счёт\" меньше 5 символов.";
                    break;
                case 4:
                    ErrorMessage = "В поле \"Расчётный счет\" меньше 20 символов.";
                    break;
                default:
                    ErrorMessage = "В поле \"BIC\" меньше 9 символов.";
                    break;
            }
            _ = MessageBox.Show(
                ErrorMessage,
                "Ошибка ввода данных",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        StreamWriter SettlementAccountsStreamWriter;
        StreamWriter ErrorMessagesStreamWriter;
        StreamWriter CommonEventStreamWriter;
        private void LoggingModule()
        {
            string DirectoryCreation;
            if (!Directory.Exists("logs"))
            {
                DirectoryInfo di = Directory.CreateDirectory("logs");
                // Composite formatting
                DirectoryCreation = string.Format("The directory was created successfully at {0}.", Directory.GetCreationTime("logs"));
            }
            else
            {
                DirectoryCreation = string.Format("The directory was already created at {0}.", Directory.GetCreationTime("logs"));
            }
            SettlementAccountsStreamWriter = File.AppendText("SettlementAccounts.txt");
            ErrorMessagesStreamWriter = File.AppendText("ErrorMessages.txt");
            CommonEventStreamWriter = File.AppendText("CommonEvent.txt");

            CommonEventStreamWriter.WriteLine(DirectoryCreation);
        }

        private void AddNoteToLogFile(string LogType, string LogMessage)
        {

        }

        private void GenerateAccount(object sender, RoutedEventArgs e)
        {
            GenerateSettlementAccount();
        }
        private void KeyingAccount(object sender, RoutedEventArgs e)
        {
            SettlementAccountСorrection();
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
