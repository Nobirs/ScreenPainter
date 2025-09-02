namespace TestLab1
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();


            //// Показываем форму загрузки
            //using (var loadingForm = new LoadingForm())
            //{
            //    if (loadingForm.ShowDialog() == DialogResult.OK)
            //    {
            //        // Если загрузка завершилась успешно, запускаем главную форму
            //        Application.Run(new Form1());
            //    }
            //}

            // Сначала показываем форму загрузки
            var loadingForm = new LoadingForm();
            loadingForm.ShowDialog();

            // После закрытия формы загрузки запускаем главную форму
            Application.Run(new NonOpacityForm());
        }
    }
}