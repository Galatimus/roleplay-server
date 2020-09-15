﻿using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Roleplay.Entities;
using Roleplay.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;

namespace Roleplay
{
    public class Server : Resource
    {
        Timer TimerPrincipal { get; set; }

        public override void OnStart()
        {
            Alt.OnPlayerConnect += OnPlayerConnect;
            Alt.OnPlayerDisconnect += OnPlayerDisconnect;
            Alt.OnPlayerDead += OnPlayerDead;
            Alt.OnWeaponDamage += OnWeaponDamage;
            Alt.OnPlayerDamage += OnPlayerDamage;
            Alt.OnClient<IPlayer, string>("OnPlayerChat", OnPlayerChat);
            Alt.OnClient<IPlayer, string, string>("EntrarUsuario", EntrarUsuario);
            Alt.OnClient<IPlayer, string, string, string, string>("RegistrarUsuario", RegistrarUsuario);
            Alt.OnClient<IPlayer>("ListarPersonagens", ListarPersonagens);
            Alt.OnClient<IPlayer, int>("SelecionarPersonagem", SelecionarPersonagem);
            Alt.OnClient<IPlayer, string, string, string, string>("CriarPersonagem", CriarPersonagem);
            Alt.OnClient<IPlayer>("ListarPlayers", ListarPlayers);
            Alt.OnClient<IPlayer, int, string, int, int, int, int, int, int>("ComprarVeiculo", ComprarVeiculo);
            Alt.OnClient<IPlayer, string>("ComprarConveniencia", ComprarConveniencia);
            Alt.OnClient<IPlayer, string, int>("AdicionarContatoCelular", AdicionarContatoCelular);
            Alt.OnClient<IPlayer, int>("RemoverContatoCelular", RemoverContatoCelular);
            Alt.OnClient<IPlayer, int>("PagarMulta", PagarMulta);
            Alt.OnClient<IPlayer, int, uint>("PegarItemArmario", PegarItemArmario);
            Alt.OnClient<IPlayer, int, string, int>("EntregarArma", EntregarArma);
            Alt.OnClient<IPlayer, string, string, string>("AtualizarInformacoes", AtualizarInformacoes);
            Alt.OnClient<IPlayer, IVehicle, string, object>("SetVehicleMeta", SetVehicleMeta);
            Alt.OnClient<IPlayer>("DevolverItensArmario", DevolverItensArmario);
            Alt.OnClient<IPlayer, int, int>("SpawnarVeiculoFaccao", SpawnarVeiculoFaccao);
            Alt.OnClient<IPlayer, int, int, int>("ConfirmarBarbearia", ConfirmarBarbearia);

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture =
                  CultureInfo.GetCultureInfo("pt-BR");

            var config = JsonConvert.DeserializeObject<Configuracao>(File.ReadAllText("settings.json"));
            Global.MaxPlayers = config.MaxPlayers;
            Global.ConnectionString = $"Server={config.DBHost};Database={config.DBName};Uid={config.DBUser};Password={config.DBPassword}";

            using (var context = new DatabaseContext())
            {
                context.Database.ExecuteSqlRaw("UPDATE Personagens SET Online=0");
                Console.WriteLine("Status online dos personagens limpo");

                Global.Parametros = context.Parametros.FirstOrDefault();
                Console.WriteLine("Parametros carregados");

                Global.Blips = context.Blips.ToList();
                Console.WriteLine($"Blips: {Global.Blips.Count}");

                Global.Faccoes = context.Faccoes.ToList();
                Console.WriteLine($"Faccoes: {Global.Faccoes.Count}");

                Global.Ranks = context.Ranks.ToList();
                Console.WriteLine($"Ranks: {Global.Ranks.Count}");

                Global.Propriedades = context.Propriedades.ToList();
                foreach (var x in Global.Propriedades)
                    x.CriarIdentificador();
                Console.WriteLine($"Propriedades: {Global.Propriedades.Count}");

                Global.Precos = context.Precos.ToList();
                Console.WriteLine($"Precos: {Global.Precos.Count}");

                Global.Pontos = context.Pontos.ToList();
                foreach (var x in Global.Pontos)
                    x.CriarIdentificador();
                Console.WriteLine($"Pontos: {Global.Pontos.Count}");

                Global.Armarios = context.Armarios.ToList();
                foreach (var x in Global.Armarios)
                    x.CriarIdentificador();
                Console.WriteLine($"Armarios: {Global.Armarios.Count}");

                Global.ArmariosItens = context.ArmariosItens.ToList();
                Console.WriteLine($"ArmariosItens: {Global.ArmariosItens.Count}");

                context.Database.ExecuteSqlRaw("UPDATE SOSs SET DataResposta = now(), TipoResposta = 3 WHERE DataResposta is null");
                Console.WriteLine("SOSs limpos");
            }

            foreach (var c in Global.Concessionarias)
                Functions.CriarTextDraw($"{c.Nome}\n~w~Use /comprar", c.PosicaoCompra, 5, 0.4f, 4, new Rgba(254, 189, 12, 255), 0);
            Console.WriteLine($"Concessionarias: {Global.Concessionarias.Count}");

            foreach (var c in Global.Empregos)
            {
                var nome = Functions.ObterDisplayEnum(c.Tipo);
                Functions.CriarTextDraw($"Emprego de {nome}\n~w~Use /emprego para se tornar um {nome.ToLower()}", c.Posicao, 5, 0.4f, 4, new Rgba(254, 189, 12, 255), 0);
            }
            Console.WriteLine($"Empregos: {Global.Empregos.Count}");

            Functions.CriarTextDraw("Prisão\n~w~Use /prender", Constants.PosicaoPrisao, 5, 0.4f, 4, new Rgba(254, 189, 12, 255), 0);

            TimerPrincipal = new Timer(60000);
            TimerPrincipal.Elapsed += TimerPrincipal_Elapsed;
            TimerPrincipal.Start();
        }

        public override void OnStop()
        {
            TimerPrincipal?.Stop();
            foreach (var p in Global.PersonagensOnline.Where(x => x.Codigo > 0))
                Functions.SalvarPersonagem(p);
        }

        private void OnPlayerConnect(IPlayer player, string reason)
        {
            player.SetDateTime(DateTime.Now);
            player.SetWeather(Global.Weather);
            player.Spawn(new Position(0f, 0f, 0f));

            using var context = new DatabaseContext();

            if (!Functions.VerificarBanimento(player, context.Banimentos.FirstOrDefault(x => (x.SocialClub == (long)player.SocialClubId && x.SocialClub != 0)
                || x.HardwareIdHash == (long)player.HardwareIdHash
                || x.HardwareIdExHash == (long)player.HardwareIdExHash)))
                return;

            player.Emit("Server:Login", context.Usuarios.FirstOrDefault(x => (x.SocialClubRegistro == (long)player.SocialClubId && x.SocialClubRegistro != 0)
                || x.HardwareIdHashRegistro == (long)player.HardwareIdHash
                || x.HardwareIdExHashRegistro == (long)player.HardwareIdExHash)?.Nome ?? string.Empty);
        }

        private void OnPlayerDisconnect(IPlayer player, string reason)
        {
            var p = Functions.ObterPersonagem(player);
            if (p?.Codigo > 0)
            {
                Functions.GravarLog(TipoLog.Saida, reason, p, null);
                Functions.SalvarPersonagem(p, false);
            }

            Global.PersonagensOnline.RemoveAll(x => x.Player?.HardwareIdHash == player.HardwareIdHash);
        }

        private void OnPlayerDead(IPlayer player, IEntity killer, uint weapon)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            Functions.GravarLog(TipoLog.Morte, JsonConvert.SerializeObject(p.Ferimentos), p,
                killer is IPlayer playerKiller ? Functions.ObterPersonagem(playerKiller) : null);

            Functions.EnviarMensagem(player, TipoMensagem.Nenhum, "Você foi gravemente ferido! Os médicos deverão chegar em até 3 minutos.");

            p.TimerFerido?.Stop();
            p.TimerFerido = new TagTimer(180000)
            {
                Tag = p.Codigo,
            };
            p.TimerFerido.Elapsed += TimerFerido_Elapsed;
            p.TimerFerido.Start();
            p.Player.Spawn(p.Player.Position);
            p.PlayAnimation("misslamar1dead_body", "dead_idle", (int)Constants.AnimationFlags.Loop);
            p.Player.Emit("player:toggleFreeze", true);
        }

        private void TimerFerido_Elapsed(object sender, ElapsedEventArgs e)
        {
            var timer = (TagTimer)sender;
            timer.ElapsedCount++;

            var p = Global.PersonagensOnline.FirstOrDefault(x => x.Codigo == (int)timer.Tag);
            if (p == null)
            {
                timer?.Stop();
                return;
            }

            Functions.EnviarMensagem(p.Player, TipoMensagem.Nenhum, "Digite /aceitartratamento para que você receba os cuidados dos médicos.");
            Functions.EnviarMensagem(p.Player, TipoMensagem.Nenhum, "Digite /aceitarck para aplicar CK no seu personagem. ESSA OPERAÇÃO É IRREVERSÍVEL.");
            timer?.Stop();
        }

        private void OnPlayerChat(IPlayer player, string message)
        {
            if (message[0] != '/')
            {
                Functions.EnviarMensagemChat(Functions.ObterPersonagem(player), message, TipoMensagemJogo.ChatICNormal);
                return;
            }

            try
            {
                var split = message.Split(" ");
                var cmd = split[0].Replace("/", string.Empty).Trim().ToLower();
                var method = Assembly.GetExecutingAssembly().GetTypes()
                    .SelectMany(x => x.GetMethods())
                    .Where(x => x.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0
                    && (x.GetCustomAttribute<CommandAttribute>().Command.ToLower() == cmd
                        || x.GetCustomAttribute<CommandAttribute>().Alias.ToLower() == cmd))
                    .FirstOrDefault();
                if (method == null)
                {
                    Functions.EnviarMensagem(player, TipoMensagem.Erro, $"O comando {message} não existe. Digite /ajuda para visualizar os comandos disponíveis.");
                    return;
                }

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
                        arr.Add(player);
                    }
                    else
                    {
                        if (split.Length <= index)
                            continue;

                        var p = split[index];

                        if (x.ParameterType == typeof(int))
                        {
                            int.TryParse(p, out int val);
                            if (val == 0 && p != "0")
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
                        else if (x.ParameterType == typeof(float))
                        {
                            float.TryParse(p, out float val);
                            if (val == 0 && p != "0")
                                continue;

                            arr.Add(val);
                        }
                    }
                }

                if (methodParams.Length != arr.Count)
                {
                    Functions.EnviarMensagem(player, TipoMensagem.Erro, $"Os parâmetros do comando não foram informados corretamente. Use: {command.HelpText}");
                    return;
                }

                method.Invoke(obj, arr.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonConvert.SerializeObject(ex));
                Functions.EnviarMensagem(player, TipoMensagem.Erro, "Não foi possível interpretar o comando.");
            }
        }

        private bool OnWeaponDamage(IPlayer player, IEntity target, uint weapon, ushort damage, Position shotOffset, BodyPart bodyPart)
        {
            if (!(target is IPlayer))
                return true;

            var p = Functions.ObterPersonagem((IPlayer)target);
            if (p == null)
                return false;

            p.Ferimentos.Add(new Ferimento()
            {
                Data = DateTime.Now,
                Arma = weapon,
                Dano = damage,
                BodyPart = (sbyte)bodyPart,
                CodigoAttacker = Functions.ObterPersonagem(player)?.Codigo ?? 0,
            });

            return true;
        }

        private void OnPlayerDamage(IPlayer player, IEntity attacker, uint weapon, ushort damage)
        {
            if (Enum.GetValues(typeof(WeaponModel)).Cast<WeaponModel>().Any(x => (uint)x == weapon))
                return;

            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            var ferimento = new Ferimento()
            {
                Data = DateTime.Now,
                Arma = weapon,
                Dano = damage,
            };

            if (attacker is IPlayer playerAttacker)
                ferimento.CodigoAttacker = Functions.ObterPersonagem(playerAttacker)?.Codigo ?? 0;
            else if (attacker is IVehicle vehicleAttacker)
                ferimento.CodigoAttacker = Functions.ObterPersonagem(vehicleAttacker.Driver)?.Codigo ?? 0;

            p.Ferimentos.Add(ferimento);
        }

        private void TimerPrincipal_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var p in Global.PersonagensOnline.Where(x => x.Codigo > 0))
                Functions.SalvarPersonagem(p);
        }

        #region Server
        private void EntrarUsuario(IPlayer player, string usuario, string senha)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
            {
                player.Emit("Server:MostrarErro", "Verifique se todos os campos foram preenchidos corretamente.");
                return;
            }

            var senhaCriptografada = Functions.Criptografar(senha);
            using var context = new DatabaseContext();
            var user = context.Usuarios.FirstOrDefault(x => x.Nome == usuario && x.Senha == senhaCriptografada);
            if (user == null)
            {
                player.Emit("Server:MostrarErro", "Usuário ou senha inválidos.");
                return;
            }

            if (!Functions.VerificarBanimento(player, context.Banimentos.FirstOrDefault(x => x.Usuario == user.Codigo)))
                return;

            user.DataUltimoAcesso = DateTime.Now;
            user.IPUltimoAcesso = Functions.ObterIP(player);
            user.SocialClubUltimoAcesso = (long)player.SocialClubId;
            user.HardwareIdHashUltimoAcesso = (long)player.HardwareIdHash;
            user.HardwareIdExHashUltimoAcesso = (long)player.HardwareIdExHash;
            context.Usuarios.Update(user);
            context.SaveChanges();

            Global.PersonagensOnline.Add(new Personagem()
            {
                UsuarioBD = user,
                Player = player,
            });

            ListarPersonagens(player);
        }

        private void ListarPersonagens(IPlayer player)
        {
            var p = Functions.ObterPersonagem(player);

            using var context = new DatabaseContext();
            player.Emit("Server:ListarPersonagens", p.UsuarioBD.Nome,
                JsonConvert.SerializeObject(context.Personagens.Where(x => x.Usuario == p.UsuarioBD.Codigo && x.DataMorte == null)
                    .OrderByDescending(x => x.Codigo)
                    .Select(x => new { x.Codigo, x.Nome })), Global.Parametros.SlotsPersonagens);
        }

        private void SelecionarPersonagem(IPlayer player, int id)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null || p?.ID > 0)
                return;

            using var context = new DatabaseContext();
            var personagem = context.Personagens.FirstOrDefault(x => x.Codigo == id && x.Usuario == p.UsuarioBD.Codigo && x.DataMorte == null);
            Functions.EnviarMensagem(player, TipoMensagem.Nenhum, $"Olá {{{Global.CorAmarelo}}}{p.UsuarioBD.Nome}{{#FFFFFF}}, que bom te ver por aqui! Seu último login foi em {{{Global.CorAmarelo}}}{personagem.DataUltimoAcesso}{{#FFFFFF}}.");
            personagem.DataUltimoAcesso = DateTime.Now;
            personagem.IPUltimoAcesso = Functions.ObterIP(player);
            personagem.SocialClubUltimoAcesso = (long)player.SocialClubId;
            personagem.ID = Functions.ObterNovoID();
            personagem.Online = true;
            personagem.HardwareIdHashUltimoAcesso = (long)player.HardwareIdHash;
            personagem.HardwareIdExHashUltimoAcesso = (long)player.HardwareIdExHash;
            context.Personagens.Update(personagem);
            context.SaveChanges();

            var user = p.UsuarioBD;
            var index = Global.PersonagensOnline.IndexOf(p);
            Global.PersonagensOnline[index] = personagem;
            Global.PersonagensOnline[index].Player = player;
            Global.PersonagensOnline[index].UsuarioBD = user;

            Functions.LogarPersonagem(player, personagem);
        }

        private void RegistrarUsuario(IPlayer player, string usuario, string email, string senha, string senha2)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(senha2))
            {
                player.Emit("Server:MostrarErro", "Verifique se todos os campos foram preenchidos corretamente.");
                return;
            }

            if (usuario.Contains(" "))
            {
                player.Emit("Server:MostrarErro", "Usuário não pode ter espaços.");
                return;
            }

            if (usuario.Length > 25)
            {
                player.Emit("Server:MostrarErro", "Usuário não pode ter mais que 25 caracteres.");
                return;
            }

            if (email.Length > 100)
            {
                player.Emit("Server:MostrarErro", "Email não pode ter mais que 100 caracteres.");
                return;
            }

            if (senha != senha2)
            {
                player.Emit("Server:MostrarErro", "Senhas não são iguais.");
                return;
            }

            if (!Functions.ValidarEmail(email))
            {
                player.Emit("Server:MostrarErro", "E-mail não está um formato válido.");
                return;
            }

            using (var context = new DatabaseContext())
            {
                if (context.Usuarios.Any(x => (x.SocialClubRegistro == (long)player.SocialClubId && x.SocialClubRegistro != 0)
                    || x.HardwareIdHashRegistro == (long)player.HardwareIdHash
                    || x.HardwareIdExHashRegistro == (long)player.HardwareIdExHash))
                {
                    player.Emit("Server:MostrarErro", $"Você já possui um usuário.");
                    return;
                }

                if (context.Usuarios.Any(x => x.Nome == usuario))
                {
                    player.Emit("Server:MostrarErro", $"Usuário {usuario} já existe.");
                    return;
                }

                if (context.Usuarios.Any(x => x.Email == email))
                {
                    player.Emit("Server:MostrarErro", $"Email {email} já está sendo utilizado.");
                    return;
                }

                var user = new Usuario()
                {
                    Nome = usuario,
                    Email = email,
                    Senha = Functions.Criptografar(senha),
                    SocialClubRegistro = (long)player.SocialClubId,
                    SocialClubUltimoAcesso = (long)player.SocialClubId,
                    IPRegistro = Functions.ObterIP(player),
                    IPUltimoAcesso = Functions.ObterIP(player),
                    HardwareIdHashRegistro = (long)player.HardwareIdHash,
                    HardwareIdExHashRegistro = (long)player.HardwareIdExHash,
                    HardwareIdHashUltimoAcesso = (long)player.HardwareIdHash,
                    HardwareIdExHashUltimoAcesso = (long)player.HardwareIdExHash,
                };
                context.Usuarios.Add(user);
                context.SaveChanges();
            }

            EntrarUsuario(player, usuario, senha);
        }

        private void CriarPersonagem(IPlayer player, string nome, string sobrenome, string sexo, string dataNascimento)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null || p.ID > 0)
                return;

            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(sobrenome) || string.IsNullOrWhiteSpace(sexo)
                || string.IsNullOrWhiteSpace(dataNascimento))
            {
                player.Emit("Server:MostrarErro", "Verifique se todos os campos foram preenchidos corretamente.");
                return;
            }

            var nomeCompleto = $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nome)} {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sobrenome)}";
            if (nomeCompleto.Length > 25)
            {
                player.Emit("Server:MostrarErro", "Nome do personagem não pode possuir mais que 25 caracteres.");
                return;
            }

            DateTime.TryParse(dataNascimento, out DateTime dtNascimento);
            if (dtNascimento == DateTime.MinValue)
            {
                player.Emit("Server:MostrarErro", "Data de Nascimento não foi informada corretamente.");
                return;
            }

            var anos = (DateTime.Now.Date - dtNascimento).TotalDays / 365;
            if (anos < 18 || anos > 90)
            {
                player.Emit("Server:MostrarErro", "Personagem precisa ter entre 18 e 90 anos.");
                return;
            }

            using var context = new DatabaseContext();
            if (context.Personagens.Any(x => x.Nome == nomeCompleto))
            {
                player.Emit("Server:MostrarErro", $"Personagem {nomeCompleto} já existe.");
                return;
            }

            var personagem = new Personagem()
            {
                Nome = nomeCompleto,
                Usuario = p.UsuarioBD.Codigo,
                Sexo = sexo,
                DataNascimento = dtNascimento,
                SocialClubRegistro = (long)player.SocialClubId,
                SocialClubUltimoAcesso = (long)player.SocialClubId,
                IPRegistro = Functions.ObterIP(player),
                IPUltimoAcesso = Functions.ObterIP(player),
                ID = Functions.ObterNovoID(),
                Skin = (long)(sexo == "M" ? PedModel.FreemodeMale01 : PedModel.FreemodeFemale01),
                InformacoesPersonalizacao = JsonConvert.SerializeObject(p.Personalizacao),
                HardwareIdHashRegistro = (long)player.HardwareIdHash,
                HardwareIdExHashRegistro = (long)player.HardwareIdExHash,
                HardwareIdHashUltimoAcesso = (long)player.HardwareIdHash,
                HardwareIdExHashUltimoAcesso = (long)player.HardwareIdExHash,
            };

            context.Personagens.Add(personagem);
            context.SaveChanges();

            context.PersonagensContatos.AddRange(new List<PersonagemContato>()
            {
                new PersonagemContato()
                {
                    Codigo = personagem.Codigo,
                    Celular = 911,
                    Nome = "Central de Emergência",
                },
                new PersonagemContato()
                {
                    Codigo = personagem.Codigo,
                    Celular = 5555555,
                    Nome = "Dowtown Cab Co.",
                },
            });
            context.SaveChanges();

            var user = p.UsuarioBD;
            var index = Global.PersonagensOnline.IndexOf(p);
            Global.PersonagensOnline[index] = personagem;
            Global.PersonagensOnline[index].Player = player;
            Global.PersonagensOnline[index].UsuarioBD = user;

            Functions.LogarPersonagem(player, personagem);
        }

        private void ListarPlayers(IPlayer player)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            var personagens = Global.PersonagensOnline.Where(x => x.ID > 0).OrderBy(x => x.ID == p.ID ? 0 : 1).ThenBy(x => x.ID)
                .Select(x => new { x.ID, x.Nome, x.Player.Ping }).ToList();

            var duty = Global.PersonagensOnline.Where(x => x.IsEmTrabalho);
            player.Emit("Server:ListarPlayers", Global.NomeServidor, JsonConvert.SerializeObject(personagens),
                duty.Count(x => x.FaccaoBD?.Tipo == TipoFaccao.Policial), duty.Count(x => x.FaccaoBD?.Tipo == TipoFaccao.Medica),
                duty.Count(x => x.Emprego == TipoEmprego.Taxista));
        }

        private void ComprarVeiculo(IPlayer player, int tipo, string veiculo, int r1, int g1, int b1, int r2, int g2, int b2)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            if (string.IsNullOrWhiteSpace(veiculo))
            {
                player.Emit("Server:MostrarErro", "Verifique se todos os campos foram preenchidos corretamente.");
                return;
            }

            var preco = Global.Precos.FirstOrDefault(x => x.Tipo == (TipoPreco)tipo && x.Nome.ToLower() == veiculo.ToLower());
            if (preco == null)
            {
                player.Emit("Server:MostrarErro", "Veículo não está disponível para compra.");
                return;
            }

            if (p.Dinheiro < preco.Valor)
            {
                player.Emit("Server:MostrarErro", "Você não possui dinheiro suficiente.");
                return;
            }

            var concessionaria = Global.Concessionarias.FirstOrDefault(x => x.Tipo == (TipoPreco)tipo);

            var veh = new Veiculo()
            {
                Personagem = p.Codigo,
                Cor1R = r1,
                Cor1G = g1,
                Cor1B = b1,
                Cor2R = r2,
                Cor2G = g2,
                Cor2B = b2,
                Modelo = veiculo,
                Placa = Functions.GerarPlacaVeiculo(),
                PosX = concessionaria.PosicaoSpawn.X,
                PosY = concessionaria.PosicaoSpawn.Y,
                PosZ = concessionaria.PosicaoSpawn.Z,
                RotX = concessionaria.RotacaoSpawn.X,
                RotY = concessionaria.RotacaoSpawn.Y,
                RotZ = concessionaria.RotacaoSpawn.Z,
            };

            using (var context = new DatabaseContext())
            {
                context.Veiculos.Add(veh);
                context.SaveChanges();
            }

            p.Dinheiro -= preco.Valor;
            p.SetDinheiro();

            Functions.EnviarMensagem(player, TipoMensagem.Sucesso, $"Você comprou {veh.Modelo} por ${preco.Valor:N0}. Use /vspawn {veh.Codigo} para spawnar.");
            player.Emit("Server:CloseView");
        }

        private void ComprarConveniencia(IPlayer player, string nome)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            var preco = Global.Precos.FirstOrDefault(x => x.Nome == nome && x.Tipo == TipoPreco.Conveniencia);
            if (p.Dinheiro < preco.Valor)
            {
                player.Emit("Server:MostrarErro", "Você não possui dinheiro suficiente.");
                return;
            }

            string strMensagem = string.Empty;
            switch (nome)
            {
                case "Celular":
                    if (p?.Celular > 0)
                    {
                        player.Emit("Server:MostrarErro", "Você já possui um celular.");
                        return;
                    }

                    using (var context = new DatabaseContext())
                    {
                        do
                        {
                            p.Celular = new Random().Next(1111111, 9999999);
                            if (p.Celular == 5555555 || context.Personagens.Any(x => x.Celular == p.Celular))
                                p.Celular = 0;

                        } while (p.Celular == 0);
                    }

                    p.Dinheiro -= preco.Valor;
                    p.SetDinheiro();

                    strMensagem = $"Você comprou um celular! Seu número é: {p.Celular}";
                    break;
                case "Rádio Comunicador":
                    if (p?.CanalRadio > -1)
                    {
                        player.Emit("Server:MostrarErro", "Você já possui um rádio comunicador.");
                        return;
                    }

                    p.CanalRadio = p.CanalRadio2 = p.CanalRadio3 = 0;
                    p.Dinheiro -= preco.Valor;
                    p.SetDinheiro();

                    strMensagem = $"Você comprou um rádio comunicador.";
                    break;
            }

            player.Emit("Server:MostrarSucesso", strMensagem);
        }

        private void AdicionarContatoCelular(IPlayer player, string nome, int celular)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            if (string.IsNullOrWhiteSpace(nome) || celular == 0)
            {
                player.Emit("Server:MostrarErro", "Verifique se os campos foram preenchidos corretamente.");
                return;
            }

            if (nome.Length > 25)
            {
                player.Emit("Server:MostrarErro", "Nome não pode ter mais que 25 caracteres.");
                return;
            }

            var contato = p.Contatos.FirstOrDefault(x => x.Celular == celular);
            if (contato == null)
            {
                p.Contatos.Add(new PersonagemContato()
                {
                    Codigo = p.Codigo,
                    Nome = nome,
                    Celular = celular
                });
                player.Emit("Server:AtualizarCelular", JsonConvert.SerializeObject(p.Contatos.OrderBy(x => x.Nome).ToList()), $"Contato {celular} adicionado com sucesso.");
            }
            else
            {
                p.Contatos[p.Contatos.IndexOf(contato)].Nome = nome;
                player.Emit("Server:AtualizarCelular", JsonConvert.SerializeObject(p.Contatos.OrderBy(x => x.Nome).ToList()), $"Contato {celular} editado com sucesso.");
            }
        }

        private void RemoverContatoCelular(IPlayer player, int celular)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            p.Contatos.RemoveAll(x => x.Celular == celular);
            player.Emit("Server:AtualizarCelular", JsonConvert.SerializeObject(p.Contatos.OrderBy(x => x.Nome).ToList()), $"Celular {celular} removido dos contatos.");
        }

        private void PagarMulta(IPlayer player, int codigo)
        {
            var p = Functions.ObterPersonagem(player);
            if (p == null)
                return;

            using (var context = new DatabaseContext())
            {
                var multa = context.Multas.FirstOrDefault(x => x.Codigo == codigo);
                if (p.Dinheiro < multa.Valor)
                {
                    player.Emit("Server:MostrarErro", "Você não possui dinheiro suficiente.");
                    return;
                }

                multa.DataPagamento = DateTime.Now;
                context.Multas.Update(multa);
                context.SaveChanges();

                p.Dinheiro -= multa.Valor;
                p.SetDinheiro();
            }

            Functions.EnviarMensagem(player, TipoMensagem.Sucesso, $"Você pagou a multa {codigo}.");
            player.Emit("Server:CloseView");
        }

        private void PegarItemArmario(IPlayer player, int armario, uint weapon)
        {
            var arma = Global.ArmariosItens.FirstOrDefault(x => x.Codigo == armario && x.Arma == weapon);
            if ((arma?.Estoque ?? 0) == 0)
            {
                player.Emit("Server:MostrarErro", $"O item não possui estoque.");
                return;
            }

            var p = Functions.ObterPersonagem(player);
            if (p.Rank < arma.Rank)
            {
                player.Emit("Server:MostrarErro", $"Você não possui autorização para pegar o item.");
                return;
            }

            player.GiveWeapon(weapon, arma.Municao, false);
            player.SetWeaponTintIndex(weapon, (byte)arma.Pintura);
            var componentes = JsonConvert.DeserializeObject<List<uint>>(arma.Componentes);
            foreach (var x in componentes)
                player.AddWeaponComponent(weapon, x);

            p.Armas.Add(new PersonagemArma()
            {
                Codigo = p.Codigo,
                Arma = arma.Arma,
                Municao = arma.Municao,
                Pintura = arma.Pintura,
                Componentes = arma.Componentes,
            });

            arma.Estoque--;
            using var context = new DatabaseContext();
            context.ArmariosItens.Update(arma);
            context.SaveChanges();

            var itens = Global.ArmariosItens.Where(x => x.Codigo == armario).OrderBy(x => x.Rank).ThenBy(x => x.Arma)
            .Select(x => new
            {
                Arma = ((WeaponModel)x.Arma).ToString(),
                Item = x.Arma,
                x.Municao,
                x.Estoque,
                Rank = Global.Ranks.FirstOrDefault(y => y.Faccao == p.Faccao && y.Codigo == x.Rank).Nome,
            }).ToList();

            player.Emit("Server:AtualizarArmario", armario, p.FaccaoBD.Nome, JsonConvert.SerializeObject(itens), p.FaccaoBD.Tipo == TipoFaccao.Policial || p.FaccaoBD.Tipo == TipoFaccao.Medica, $"Você equipou {(WeaponModel)weapon}.");
            Functions.GravarLog(TipoLog.Arma, $"/armario {JsonConvert.SerializeObject(arma)}", p, null);
        }

        private void EntregarArma(IPlayer player, int codigo, string weapon, int municao)
        {
            var p = Functions.ObterPersonagem(player);

            var target = Global.PersonagensOnline.FirstOrDefault(x => x.Codigo == codigo);
            if (target == null)
                return;

            uint.TryParse(weapon, out uint arma);
            var wep = p.Armas.FirstOrDefault(x => x.Arma == arma);

            player.Emit("RemoveWeapon", arma);
            p.Armas.Remove(wep);

            wep.Codigo = target.Codigo;
            target.Player.GiveWeapon(arma, municao, true);
            target.Player.SetWeaponTintIndex(arma, (byte)wep.Pintura);
            var componentes = JsonConvert.DeserializeObject<List<uint>>(wep.Componentes);
            foreach (var x in componentes)
                target.Player.AddWeaponComponent(arma, x);
            target.Armas.Add(wep);

            Functions.EnviarMensagem(player, TipoMensagem.Sucesso, $"Você deu {(WeaponModel)arma} com {municao} de munição para {target.NomeIC}.");
            Functions.EnviarMensagem(target.Player, TipoMensagem.Sucesso, $"{p.NomeIC} te deu {(WeaponModel)arma} com {municao} de munição.");
            Functions.GravarLog(TipoLog.Arma, $"/entregararma {arma}", p, target);
        }

        private void AtualizarInformacoes(IPlayer player, string areaName, string zoneName, string armas)
        {
            var p = Functions.ObterPersonagem(player);
            p.AreaName = areaName;
            p.ZoneName = zoneName;
            p.StringArmas = armas;
        }

        private void SetVehicleMeta(IPlayer player, IVehicle vehicle, string meta, object value) => vehicle.SetStreamSyncedMetaData(meta, value);

        private void DevolverItensArmario(IPlayer player)
        {
            var p = Functions.ObterPersonagem(player);

            foreach (var x in p.Armas)
                player.Emit("RemoveWeapon", (uint)x.Arma);

            p.Armas = new List<PersonagemArma>();

            Functions.EnviarMensagem(player, TipoMensagem.Sucesso, $"Você devolveu seus itens no armário.", notify: true);
            Functions.GravarLog(TipoLog.Arma, $"/armario DevolverItensArmario", p, null);
        }

        private void SpawnarVeiculoFaccao(IPlayer player, int codigoPonto, int veiculo)
        {
            var p = Functions.ObterPersonagem(player);

            if (Global.Veiculos.Any(x => x.Codigo == veiculo))
            {
                Functions.EnviarMensagem(player, TipoMensagem.Erro, "Veículo já está spawnado.", notify: true);
                return;
            }

            using var context = new DatabaseContext();
            var veh = context.Veiculos.FirstOrDefault(x => x.Codigo == veiculo);
            veh.PosX = player.Position.X;
            veh.PosY = player.Position.Y;
            veh.PosZ = player.Position.Z;
            veh.NomeEncarregado = p.Nome;

            var ponto = Global.Pontos.FirstOrDefault(x => x.Codigo == codigoPonto);
            if (ponto.Tipo == TipoPonto.SpawnVeiculosFaccao)
            {
                var rot = JsonConvert.DeserializeObject<Rotation>(ponto.Configuracoes);
                veh.RotX = rot.Roll;
                veh.RotY = rot.Pitch;
                veh.RotZ = rot.Yaw;
            }

            veh.Spawnar();
            veh.Vehicle.LockState = VehicleLockState.Unlocked;
            player.Emit("setPedIntoVehicle", veh.Vehicle, -1);
            player.Emit("Server:CloseView");
            Functions.EnviarMensagem(player, TipoMensagem.Sucesso, $"Você spawnou o veículo {veiculo}.", notify: true);
        }

        private void ConfirmarBarbearia(IPlayer player, int cabelo, int cor1, int cor2)
        {
            var p = Functions.ObterPersonagem(player);

            p.SetClothes(2, cabelo, 0, false);
            p.Personalizacao.CabeloCor1 = cor1;
            p.Personalizacao.CabeloCor2 = cor2;

            p.Dinheiro -= Global.Parametros.ValorBarbearia;
            p.SetDinheiro();
            Functions.EnviarMensagem(player, TipoMensagem.Sucesso, $"Você pagou ${Global.Parametros.ValorBarbearia:N0} na barbearia.");
        }
        #endregion
    }
}