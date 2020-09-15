﻿using Roleplay;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Timers;

namespace Testador
{
    class Program
    {
        static BackgroundWorker bk = new BackgroundWorker();
        static System.Timers.Timer timer { get; set; } = new System.Timers.Timer(1000);

        static void Main(string[] args)
        {
            bk = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true
            };
            bk.DoWork += Bk_DoWork;
            bk.RunWorkerCompleted += Bk_RunWorkerCompleted;

            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            var msg = Console.ReadLine();
            //OnPlayerChat(Console.ReadLine());
        }

        private static void Bk_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var completo = 1;
        }

        private static void Bk_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(10000);

            var eae = 1;
        }

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!bk.IsBusy)
                bk.RunWorkerAsync();
        }

        [Command("a", "/a (mensagem)", GreedyArg = true)]
        public static void CMD_a(string player, string msg)
        {
            var a = "oi";
        }

        private static void OnPlayerChat(string message)
        {
            if (message[0] != '/')
            {
                //Functions.EnviarMensagemChat(Functions.ObterPersonagem(player), message, TipoMensagemJogo.ChatICNormal);
                return;
            }

            var split = message.Split(" ");
            var cmd = split[0].Replace("/", string.Empty).Trim().ToLower();
            var methodx = Assembly.GetExecutingAssembly().GetTypes()
                .SelectMany(x => x.GetMethods())
                .Where(x => x.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0).ToList();
            var method = methodx.Where(x => x.GetCustomAttribute<CommandAttribute>().Command.ToLower() == cmd || x.GetCustomAttribute<CommandAttribute>().Alias.ToLower() == cmd).FirstOrDefault();
            if (method == null)
            {
                //Functions.EnviarMensagem(player, TipoMensagem.Erro, $"O comando {message} não existe. Digite /ajuda para visualizar os comandos disponíveis.");
                return;
            }

            try
            {
                var methodParams = method.GetParameters();
                var obj = Activator.CreateInstance(method.DeclaringType);
                var command = method.GetCustomAttribute<CommandAttribute>();

                var arr = new List<object>();

                var list = methodParams.ToList();
                foreach (var x in list)
                {
                    var index = list.IndexOf(x);
                    if (index == 0)
                    {
                        arr.Add("player");
                    }
                    else
                    {
                        if (split.Length <= index)
                            continue;

                        var p = split[index];

                        if (x.ParameterType == typeof(int))
                        {
                            int.TryParse(p, out int val);
                            if (val == 0)
                                continue;

                            arr.Add(val);
                        }
                        else if (x.ParameterType == typeof(string))
                        {
                            if (string.IsNullOrWhiteSpace(p))
                                continue;

                            if (command.GreedyArg && index + 1 == list.Count)
                                p = string.Join(" ", split.Skip(index).Take(split.Length - index));

                            arr.Add(p);
                        }
                    }
                }

                if (methodParams.Length != arr.Count)
                {
                    //Functions.EnviarMensagem(player, TipoMensagem.Erro, $"Os parâmetros do comando não foram informados corretamente. Use: {command.HelpText}");
                    return;
                }

                method.Invoke(obj, arr.ToArray());
            }
            catch (Exception ex)
            {
                var a = ex;
                //Console.WriteLine(JsonConvert.SerializeObject(ex));
                //Functions.EnviarMensagem(player, TipoMensagem.Erro, "Não foi possível interpretar o comando.");
            }
        }
    }
}
