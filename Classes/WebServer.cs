using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AgentSystem.Classes
{
    class WebServer
    {
        //сервер
        public HttpListener Listener = new HttpListener();
        // ключи сервера: закрытый - для работы, открытый - для возможной передачи по запросу
        private RSACryptoServiceProvider servPrivateKey = new RSACryptoServiceProvider();
        private string servPublicKey;
        //список слушаемых адресов в формате address:port
        private List<string> _url = new List<string>();
        //делегат для определения внешней функции логирования
        public delegate void LogMethod(string str);
        //делегат для определения внешней функции обработки принятой команды
        public delegate string InterpretationMethod(string str, Privileges privilege);
        //определяет, будут ли писаться логи: true - да, false - нет
        private bool isLoggingRequired = false;
        //внешняя функция логирования
        private LogMethod WriteLog = null;
        //внешняя функция обработки принятой команды
        private InterpretationMethod Interpret = null;
        //Список пользователей
        private Dictionary<string, User> _users = new Dictionary<string, User>();
        //строка, передаваемого в качестве ответа на любой GET-запрос
        private string ResponseToGetRequest;

        public WebServer(List<string> url) { AddUrl(url); }
        public WebServer(List<string> url, LogMethod log) { AddUrl(url); EnableLogging(log); }
        private void AddUrl(List<string> url)
        {
            _url = url;
            foreach (var u in _url)
                Listener.Prefixes.Add(u);
        }
        public void SetInterpretator(InterpretationMethod interpretator)
        {
            Interpret = interpretator;
        }
        public void EnableLogging(LogMethod log_method)
        {
            isLoggingRequired = true;
            WriteLog = log_method;
        }
        public void DisableLogging()
        {
            isLoggingRequired = false;
            WriteLog = null;
        }
        private void Log(string mes)
        {
            if (isLoggingRequired) WriteLog(mes);
        }
        //переопределяет список пользователей сервера
        public void AddUsers(Dictionary<string, User> users)
        {
            _users = users;
        }
        
        //считывает файл для ответа на любой GET-запрос
        public void SetResponseToGetRequest(string fpath) 
        {
            try
            {
                using (StreamReader sr = new StreamReader(fpath))
                {
                    ResponseToGetRequest = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                WriteLog($"Webserver: Не удалось прочитать файл {fpath} для ответа на GET-запрос - {e.Message}");
            }
        }

        //Считывает базу пользователей сервера из файла, при этом предыдущие сведения 
        //о пользователях теряются. Фактически переопределяет базу пользователей сервера.
        //Параметр folder - папка в текущем каталоге программы,
        //в котором хранятся публичные ключи пользователей.
        //Файл имеет формат "отдельная строка - один пользователь",
        //строка имеет формат (с начала строки): user:role, где role - число (1-админ, 2-пользователь)
        //в файле с названием типа user.pub хранится открытый RSA-ключ пользователя user
        public void AddUsersFromFile(string filename, string folder = "")
        {
            Dictionary<string, User> users = new Dictionary<string, User>();
            using (StreamReader sr = new StreamReader(filename))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    //изначально false. Если при подготовке данных очередного пользователя
                    //неясностей не возникнет, станет true, и текущий пользователь будет добавлен в список
                    bool isUserValid = false;
                    User user = new User();
                    string[] userid = line.Trim().Split(':');
                    userid[0] = userid[0].Trim();
                    //если имя пользователя существует
                    if (userid[0].Length > 0)
                    {
                        int priv;
                        bool isPrivilegeExist = int.TryParse(userid[1].Trim(), out priv);
                        user.Privilege = (Privileges)priv;
                        //если роль есть и она не равна нулю
                        if (isPrivilegeExist && user.Privilege != 0)
                        {
                            //ищем открытый ключ пользователя
                            bool isImportKeyValid = false;
                            string pubkeyfile = $"{folder}\\{userid[0]}.pub";
                            var rsa = new RSACryptoServiceProvider();
                            try
                            {
                                string keyxml;
                                using (StreamReader sk = new StreamReader(pubkeyfile))
                                {
                                    keyxml = sk.ReadToEnd();
                                }
                                rsa.FromXmlString(keyxml);
                                //проверяем: приватный ключ здесь нонсенс.
                                if (rsa.PublicOnly) isImportKeyValid = true;
                            }
                            catch (Exception e)
                            {
                                Log($"Ошибка при чтении публичного ключа {pubkeyfile}");
                                Log($"Exception: {e.Message}");
                            }
                            //если импорт ключа прошел успешно, сохраняем его
                            if (isImportKeyValid)
                            {
                                user.PublicKey = rsa.ExportParameters(false);
                                isUserValid = true;
                            }
                        }
                    }
                    if (isUserValid)
                    {
                        try
                        {
                            users.Add(userid[0], user);
                            Log($"Добавлен пользователь {userid[0]} с ролью {user.Privilege}");
                        }
                        catch
                        {
                            Log($"Попытка повторного добавления пользователя {userid[0]} с ролью {user.Privilege} проигнорирована");
                        }

                    }
                    else
                    {
                        Log($"Пользователь {userid[0]} в базу не добавлен из-за некорректного определения");
                    }
                }
            }
            //обновляем даные о пользователях (старые данные теряются)
            AddUsers(users);
        }
        public void AddUsersFromConfig(IConfiguration settings)
        {
            //получение ключей сервера из настроек
            servPublicKey = settings.GetSection("server:publickey").Value;
            string keyxml = settings.GetSection("server")["privatekey"];
            servPrivateKey.FromXmlString(keyxml);

            //формирование списка пользователей из настроек
            Dictionary<string, User> users = new Dictionary<string, User>();
            var clientlist = settings.GetSection("clients").GetChildren();
            foreach (var client in clientlist)
            {

                //изначально false. Если при подготовке данных очередного пользователя
                //неясностей не возникнет, станет true, и текущий пользователь будет добавлен в список
                bool isUserValid = false;
                User user = new User();
                string uid = client.Key.Trim();
                //если имя пользователя корректно
                if (uid.Length > 0)
                {
                    //проверка прав
                    bool isPrivilegeExist = false;
                    string uprivilege = client["privilege"].Trim().ToLower();
                    if (uprivilege == "admin" || uprivilege == "administrator")
                    {
                        user.Privilege = Privileges.ADMIN;
                        isPrivilegeExist = true;
                    }
                    if (uprivilege == "user")
                    {
                        user.Privilege = Privileges.USER;
                        isPrivilegeExist = true;
                    }
                    
                    //если права есть и они не нулевые и не серверные
                    if (isPrivilegeExist && (int)user.Privilege > 1)
                    {
                        //ищем открытый ключ пользователя
                        bool isImportKeyValid = false;
                        keyxml = client["publickey"];
                        var rsa = new RSACryptoServiceProvider();
                        try
                        {
                            rsa.FromXmlString(keyxml);
                            //проверяем: приватный ключ здесь нонсенс.
                            if (rsa.PublicOnly) isImportKeyValid = true;
                        }
                        catch (Exception e)
                        {
                            Log($"Ошибка при импорте публичного ключа пользователя {uid}");
                            Log($"Exception: {e.Message}");
                        }
                        //если импорт ключа прошел успешно, сохраняем его
                        if (isImportKeyValid)
                        {
                            user.PublicKey = rsa.ExportParameters(false);
                            isUserValid = true;
                        }
                    }
                }
                if (isUserValid)
                {
                    try
                    {
                        users.Add(uid, user);
                        Log($"Добавлен пользователь {uid} с правами {user.Privilege}");
                    }
                    catch
                    {
                        Log($"Попытка повторного добавления пользователя {uid} с ролью {user.Privilege} проигнорирована");
                    }
                }
                else
                {
                    Log($"Пользователь {uid} в базу не добавлен из-за некорректного определения");
                }
            }
            //обновляем даные о пользователях (старые данные теряются)
            AddUsers(users);
        }

        public void Start()
        {
            Listener.Start();
            _ = Listen();
            Log("Сервер запущен на прослушивание адресов:");
            foreach (var u in Listener.Prefixes) Log(u);
        }
        public void Stop()
        {
            Listener.Stop();
            Log("Сервер обработки запросов клиентов остановлен.");
        }

        private async Task Listen()
        {
            while (true)
            {
                HttpListenerContext context = await Listener.GetContextAsync();
                Thread thread = new Thread(new ParameterizedThreadStart(ClientSession));
                thread.Start(context);
            }
        }

        private void ClientSession(object client)
        {
            var context = client as HttpListenerContext;

            HttpListenerRequest request = context.Request;
            //RijndaelManaged aes = new RijndaelManaged();

            (string resp, Privileges priv, RijndaelManaged aes) = ProcessingRequest(request);
            Log(resp);
            if (request.HttpMethod != "GET" && (int)priv > 1)
            {
                string cmd = request.RawUrl.Replace(@"/","");
                resp = Interpret($"{cmd}?{resp}", priv);
                //шифрование ответа ключом AES
            }
            resp = EncryptString(resp, aes);

            HttpListenerResponse response = context.Response;
            string responseString = resp;
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            try
            {
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            catch (Exception e)
            {
                WriteLog($"Ошибка при передаче ответа клиенту: {e.Message}");
            }
        }

        private (string, Privileges, RijndaelManaged) ProcessingRequest(HttpListenerRequest request)
        {
            string res = "";
            string err = "Попытка несанкционированного доступа";
            Privileges clientPrivilege = Privileges.NO_PRIVILEGES;
            RijndaelManaged aes = null;
            if (request.HttpMethod == "GET")
            {
                if (ResponseToGetRequest == null)
                {
                    string url = request.RawUrl;
                    try
                    {
                        if (url.IndexOf('?') > -1)
                            res = $"Command:{url.Substring(1, url.IndexOf('?') - 1)}<br/>";
                        if (request.QueryString.Count > 0)
                        {
                            var qstring = request.QueryString;
                            res = $"{res} Arguments<br/>";
                            foreach (var key in qstring.AllKeys)
                            {
                                res = $"{res}{key,-12}:{qstring.GetValues(key)[0]} <br/>";
                            }
                        }
                        res = $"<html><head><meta charset='utf8'>{err}<br/>{res}</head><body></body></html>";
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("{0} Exception caught.", e);

                        res = $"{res}Исключение {e.Message}";
                    }
                }
                else
                {
                    res = ResponseToGetRequest;
                }
            }
            else
            {
                //Проверка пользователя
                //Получаем заголовки
                string uid = request.Headers.Get("Uid");    //имя пользователя: UTF8 (без преобразования)
                string ssign = request.Headers.Get("Sign"); //цифровая подпись запроса: Base64
                string saes = request.Headers.Get("AES");    //зашифрованный симметричный ключ: Base64
                string encreq = request.Headers.Get("Req");   //зашифрованный запрос: Base64
                //Аутентификация
                byte[] sign = Convert.FromBase64String(ssign);
                byte[] data = Encoding.UTF8.GetBytes(encreq);
                clientPrivilege = Auth(uid, sign, data);
                //получение ключа AES
                aes = GetUserRijndaelKey(saes);
                if (clientPrivilege != 0)
                {
                    //расшифровка запроса пользователя
                    string message = DecryptString(encreq, aes);
                    message = HttpUtility.UrlDecode(message);
                    res = message;

                    //Получаем тело запроса в тестовых целях (так-то он не нужен совсем)
                    var stream = request.InputStream;
                    string sdata;
                    using (var reader = new StreamReader(stream))
                    {
                        sdata = reader.ReadToEnd();
                    }
                    //Log($"decrypt mess:{message}");
                    //Log($"body request:{sdata}");
                    //шифрование ответа ключом AES
                    //res = EncryptString($"decrypt mess:{message}", aes);
                }
                else res = err;
            }
            return (res, clientPrivilege, aes);
        }

        //Производит аутентификацию пользователя
        //Входные параметры:
        //  key - идентификатор пользователя
        //  sign - цифровая подпись ключом RSA
        //  data - подписанные данные
        //Возвращает:
        //  NO_PRIVILEGES - если аутентификация не прошла
        //  права пользователя - если аутентификация завершилась успешно
        //  
        private Privileges Auth(string key, byte[] sign, byte[] data)
        {
            Privileges privileges = 0;
            //проверяем, есть ли такой пользователь в текущей базе сервера
            bool is_user = _users.TryGetValue(key, out User client);
            //если пользователь есть в текущей базе сервера
            if (is_user)
            {
                Log($"Запрос поступил предположительно от пользователя {key}");
                var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(client.PublicKey);
                //проверяем цифровую подпись
                bool auth = rsa.VerifyData(data, CryptoConfig.MapNameToOID("SHA512"), sign);
                //Если проверка цифровой подписи прошла успешно, значит запрос валиден
                //возвращаем права найденного пользователя
                if (auth)
                {
                    privileges = client.Privilege;
                    Log($"Запрос подписан пользователем {key}");
                }
                else Log($"Похоже, пользователь {key} - самозванец");
            }
            else
            {
                Log($"Пользователь {key} ошибся дверью (возможно, намеренно)");
            }
            return privileges;
        }

        /// <summary>
        /// Получение ключа AES
        /// </summary>
        /// <param name="skey"> строка с зашифрованным ключом </param>
        /// <returns>расшифрованный ключ</returns>
        
        private RijndaelManaged GetUserRijndaelKey(string skey) 
        {
            RijndaelManaged rjndl = new RijndaelManaged();
            rjndl.KeySize = 256;  //Длина ключа 256 бит (максимум), хотя мне кажется, что сойдет и 128
            rjndl.BlockSize = 128; //Для совместимости с AES
            rjndl.Mode = CipherMode.CBC;

            byte[] rjndl_key = Convert.FromBase64String(skey);
            //Формат принятого Rijndael-ключа:
            //0-3: 4 байта - длина зашифрованного ключа
            //4-7: 4 байта - длина вектора инициализации (IV)
            //8-...: зашифрованный ключ
            //вектор инициализации

            // Create byte arrays to contain the length values of the key and IV.
            // Создание массивов байтов, содержащих значения длины зашифрованного ключа
            // и вектора инициализации (IV).
            byte[] LenK = new byte[4];
            byte[] LenIV = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                LenK[i] = rjndl_key[i];
                LenIV[i] = rjndl_key[i + 4];
            }
            // Convert the lengths to integer values - Конвертирование длин в значения int
            int lenK = BitConverter.ToInt32(LenK, 0);
            int lenIV = BitConverter.ToInt32(LenIV, 0);
            // получение зашифрованного ключа и вектора инициализации
            byte[] KeyEncrypted = new byte[lenK];
            byte[] IV = new byte[lenIV];
            for (int i = 0; i < lenK; i++)
            {
                KeyEncrypted[i] = rjndl_key[i + 8];
            }
            for (int i = 0; i < lenIV; i++)
            {
                IV[i] = rjndl_key[i + 8 + lenK];
            }
            //расшифровка симметричного ключа Rijndael приватным ключом RSA сервера
            //var rsa = new RSACryptoServiceProvider();
            byte[] keyDecrypted = servPrivateKey.Decrypt(KeyEncrypted, false);

            rjndl.Key = keyDecrypted;
            rjndl.IV = IV;
            return rjndl;
        }
        private string EncryptString(string message, RijndaelManaged aes)
        {
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] source = Encoding.UTF8.GetBytes(message);
            byte[] dest = encryptor.TransformFinalBlock(source, 0, source.Length);
            string res = Convert.ToBase64String(dest);
            return res;
        }
        private string DecryptString(string encmessage, RijndaelManaged aes)
        {
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] source = Convert.FromBase64String(encmessage);
            byte[] dest = decryptor.TransformFinalBlock(source, 0, source.Length);
            string res = Encoding.UTF8.GetString(dest);
            return res;
        }
    }

    class User
    {
        private Privileges privilege = 0;
        public RSAParameters PublicKey;

        internal Privileges Privilege { get => privilege; set => privilege = value; }
    }

}
