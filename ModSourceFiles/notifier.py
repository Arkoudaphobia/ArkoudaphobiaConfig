import re
import time
import BasePlayer
import ConVar.Server as sv
import TOD_Sky
import UnityEngine.Random as random
from System import Action, Int32, String

DEV = False
LINE = '-' * 50
DBNAME = 'notifier-countries_db'

class notifier:

    def __init__(self):

        self.Title = 'Notifier'
        self.Version = V(2, 18, 0)
        self.Author = 'SkinN'
        self.Description = 'Server administration tool with chat based notifications'
        self.ResourceId = 797
        self.Default = {
            'SETTINGS': {
                'PREFIX': '<white>[<end> <cyan>NOTIFIER<end> <white>]<end>',
                'BROADCAST TO CONSOLE': True,
                'RULES LANGUAGE': 'AUTO',
                'HIDE ADMINS': False,
                'PLAYERS LIST ON CHAT': True,
                'PLAYERS LIST ON CONSOLE': False,
                'ADVERTS INTERVAL': 12,
                'MOTD INTERVAL': 120,
                'MOTD PREFIX': '[ <yellow>MOTD<end> ]',
                'ANNOUNCE AIRDROPS': False,
                'ANNOUNCE PATROL HELICOPTERS': False,
                'DIFFER ADMINS JOIN/LEAVE MESSAGE': False,
                'ICON PROFILE': '76561198235146288',
                'ENABLE PLUGIN ICON': False,
                'ENABLE SCHEDULED MESSAGES': True,
                'ENABLE PLAYERS DEFAULT COLORS': False,
                'ENABLE JOIN MESSAGE': True,
                'ENABLE LEAVE MESSAGE': True,
                'ENABLE WELCOME MESSAGE': True,
                'ENABLE ADVERTS': True,
                'ENABLE PLAYERS LIST': True,
                'ENABLE PLAYERS ONLINE': True,
                'ENABLE ADMINS LIST': False,
                'ENABLE PLUGINS LIST': False,
                'ENABLE RULES': True,
                'ENABLE MAP LINK': True,
                'ENABLE ADVERTS COMMAND': True,
                'ENABLE MOTD': True
            },
            'MESSAGES': {
                'JOIN MESSAGE': '<lime>{username}<end> joined the server, from <orange>{country}<end>.',
                'LEAVE MESSAGE': '<lime>{username}<end> left the server. (Reason: <white>{reason}<end>)',
                'ADMINS JOIN MESSAGE': '<#ADFF64>{username}<end> joined the server, from <orange>{country}<end>.',
                'ADMINS LEAVE MESSAGE': '<#ADFF64>{username}<end> left the server. (Reason: <white>{reason}<end>)',
                'CHECK CONSOLE': 'Check the console (press F1) for more info.',
                'PLAYERS ONLINE': 'There are <lime>{active}<end>/<lime>{maxplayers}<end> players online.',
                'ADMINS ONLINE': 'There are <cyan>{admins} Admins<end> online.',
                'PLAYERS STATS': 'Sleepers: <lime>{sleepers}<end> Alltime Players: <lime>{alltime}<end>',
                'MAP LINK': 'See where you are on the server map at: <lime>http://{ip}:{port}<end>',
                'NO RULES': 'Error, no rules found, contact the <cyan>Admins<end>.',
                'NO LANG': 'Error, <lime>{args}<end> language not supported or does not exist.',
                'NO ADMINS': 'There are no <cyan>Admins<end> online.',
                'ADVERTS INTERVAL CHANGED': 'Adverts interval changed to <lime>{minutes}<end> minutes',
                'SYNTAX ERROR': 'Syntax Error: {syntax}',
                'ADMINS LIST TITLE': 'ADMINS ONLINE',
                'PLUGINS LIST TITLE': 'SERVER PLUGINS',
                'PLAYERS LIST TITLE': 'PLAYERS LIST',
                'PLAYERS ONLINE TITLE': 'PLAYERS ONLINE',
                'RULES TITLE': 'SERVER RULES',
                'PLAYERS LIST DESC': '<orange>/players<end> <grey>-<end> List of all players in the server.',
                'ADMINS LIST DESC': '<orange>/admins<end> <grey>-<end> List of online <cyan>Admins<end> in the server.',
                'PLUGINS LIST DESC': '<orange>/plugins<end> <grey>-<end> List of plugins installed in the server.',
                'RULES DESC': '<orange>/rules<end> <grey>-<end> List of server rules.',
                'MAP LINK DESC': '<orange>/map<end> <grey>-<end> Server map url.',
                'ADVERTS DESC': '<orange>/adverts<end> <grey>-<end> Allows <cyan>Admins<end> to change the adverts interval ( i.g: /adverts 5 )',
                'PLAYERS ONLINE DESC': '<orange>/online<end> <grey>-<end> Shows the number of players and <cyan>Admins<end> online, plus a few server stats.',
                'AIRDROP CALLED': 'An <yellow>Airdrop<end> is coming, will drop at <lime>{location}<end> coordinates.',
                'HELI CALLED': 'A <yellow>Patrol Helicopter<end> is coming!'
            },
            'WELCOME MESSAGE': (
                '<size=17>Welcome <lime>{username}<end></size>',
                '<orange><size=20>•</size><end> Type <orange>/help<end> for all available commands.',
                '<orange><size=20>•</size><end> Check our server <orange>/rules<end>.',
                '<orange><size=20>•</size><end> See where you are on the server map at: <lime>http://{server.ip}:{server.port}<end>'
            ),
            'SCHEDULED MESSAGES': {
                '00:00': ('It is now <lime>{localtime}<end> of <lime>{localdate}<end>',),
                '12:00': ('It is now <lime>{localtime}<end> of <lime>{localdate}<end>',)
            },
            'ADVERTS': (
                'Want to know the available commands? Type <orange>/help<end>.',
                'Respect the server <orange>/rules<end>.',
                'This server is running <orange>Oxide 2<end>.',
                '<red>Cheat is strictly prohibited.<end>',
                'Type <orange>/map<end> for the server map link.',
                'You are playing on: <lime>{server.hostname}<end>',
                '<orange>Players Online: <lime>{players}<end> / <lime>{server.maxplayers}<end> Sleepers: <lime>{sleepers}<end><end>',
                'The game time is <lime>{gamedate} {gametime}<end>',
                'The server time and date is <lime>{localdate} {localtime}<end>'
            ),
            'MOTD': 'This server now has the new <yellow>Message Of The Day<end> feature provided by <cyan>Notifier<end>. Use <orange>/motd<end> to know server\'s daily news!',
            'COLORS': {
                'PREFIX': '#00EEEE',
                'JOIN MESSAGE': 'silver',
                'LEAVE MESSAGE': 'silver',
                'WELCOME MESSAGE': 'silver',
                'ADVERTS': 'silver',
                'SYSTEM': 'white',
                'BOARDS TITLE': 'silver',
                'SCHEDULED MESSAGES': 'silver',
                'MOTD': 'silver'
            },
            'COMMANDS': {
                'PLAYERS LIST': 'players',
                'RULES': ('rules', 'regras', 'regles'),
                'PLUGINS LIST': 'plugins',
                'ADMINS LIST': 'admins',
                'PLAYERS ONLINE': 'online',
                'MAP LINK': 'map',
                'ADVERTS COMMAND': 'adverts',
                'MOTD': 'motd'
            },
            'RULES': {
                'EN': (
                    'Cheating is strictly prohibited.',
                    'Respect all players',
                    'Avoid spam in chat.',
                    'Play fair and don\'t abuse of bugs/exploits.'
                ),
                'PT': (
                    'Usar cheats e totalmente proibido.',
                    'Respeita todos os jogadores.',
                    'Evita spam no chat.',
                    'Nao abuses de bugs ou exploits.'
                ),
                'FR': (
                    'Tricher est strictement interdit.',
                    'Respectez tous les joueurs.',
                    'Évitez le spam dans le chat.',
                    'Jouer juste et ne pas abuser des bugs / exploits.'
                ),
                'ES': (
                    'Los trucos están terminantemente prohibidos.',
                    'Respeta a todos los jugadores.',
                    'Evita el Spam en el chat.',
                    'Juega limpio y no abuses de bugs/exploits.'
                ),
                'DE': (
                    'Cheaten ist verboten!',
                    'Respektiere alle Spieler',
                    'Spam im Chat zu vermeiden.',
                    'Spiel fair und missbrauche keine Bugs oder Exploits.'
                ),
                'TR': (
                    'Hile kesinlikle yasaktır.',
                    'Tüm oyuncular Saygı.',
                    'Sohbet Spam kaçının.',
                    'Adil oynayın ve böcek / açıkları kötüye yok.'
                ),
                'IT': (
                    'Cheating è severamente proibito.',
                    'Rispettare tutti i giocatori.',
                    'Evitare lo spam in chat.',
                    'Fair Play e non abusare di bug / exploit.'
                ),
                'DK': (
                    'Snyd er strengt forbudt.',
                    'Respekter alle spillere.',
                    'Undgå spam i chatten.',
                    'Spil fair og misbrug ikke bugs / exploits.'
                ),
                'RU': (
                    'Запрещено использовать читы.',
                    'Запрещено спамить и материться.',
                    'Уважайте других игроков.',
                    'Играйте честно и не используйте баги и лазейки.'
                ),
                'NL': (
                    'Vals spelen is ten strengste verboden.',
                    'Respecteer alle spelers',
                    'Vermijd spam in de chat.',
                    'Speel eerlijk en maak geen misbruik van bugs / exploits.'
                ),
                'UA': (
                    'Обман суворо заборонено.',
                    'Поважайте всіх гравців',
                    'Щоб уникнути спаму в чаті.',
                    'Грати чесно і не зловживати помилки / подвиги.'
                ),
                'RO': (
                    'Cheaturile sunt strict interzise!',
                    'Respectați toți jucătorii!',
                    'Evitați spamul în chat!',
                    'Jucați corect și nu abuzați de bug-uri/exploituri!'
                ),
                'HU': (
                    'Csalás szigorúan tilos.',
                    'Tiszteld minden játékostársad.',
                    'Kerüld a spammolást a chaten.',
                    'Játssz tisztességesen és nem élj vissza a hibákkal.'
                )
            }
        }

    # -------------------------------------------------------------------------
    # - CONFIGURATION / DATABASE SYSTEM
    def LoadDefaultConfig(self):
        '''Hook called when there is no configuration file '''

        self.Config.clear()
        self.Config = self.Default
        self.SaveConfig()

    # -------------------------------------------------------------------------
    def UpdateConfig(self):
        '''Function to update the configuration file on plugin Init '''

        # Override config in developer mode is enabled
        if DEV: self.LoadDefaultConfig(); return

        # Remove config versioning
        if 'CONFIG_VERSION' in self.Config:

            del self.Config['CONFIG_VERSION']

        # Start configuration checks
        for section in self.Default:

            # Is section in the configuration file
            if section not in self.Config:

                # Add section to config
                self.Config[section] = self.Default[section]

            elif isinstance(self.Default[section], dict):

                # Check for sub-section
                for sub in self.Default[section]:

                    if sub not in self.Config[section]:

                        self.Config[section][sub] = self.Default[section][sub]

        self.SaveConfig()

    # -------------------------------------------------------------------------
    def save_data(self, args=None):
        '''Function to save the plugin database '''

        data.SaveData(DBNAME)

        self.con('Saving database')

    # -------------------------------------------------------------------------
    # - MESSAGE SYSTEM
    def con(self, text):
        '''Function to send a server con message '''

        if self.Config['SETTINGS']['BROADCAST TO CONSOLE']:

            print('[%s v%s] %s' % (self.Title, str(self.Version), self.scs(text, True)))

    # -------------------------------------------------------------------------
    def pcon(self, player, text, color='silver'):
        '''Function to send a message to a player console '''

        player.SendConsoleCommand(self.scs('echo <%s>%s<end>' % (color, text)))

    # -------------------------------------------------------------------------
    def say(self, text, color='silver', f=True, profile='0'):
        '''Function to send a message to all players '''

        if PLUGIN['ENABLE PLUGIN ICON']:

            profile = PLUGIN['ICON PROFILE']

        if len(self.prefix) and f:

            msg = self.scs('%s <%s>%s<end>' % (self.prefix, color, text))

        else:

            msg = self.scs('<%s>%s<end>' % (color, text))

        rust.BroadcastChat(msg, None, profile)

        self.con(text)

    # -------------------------------------------------------------------------
    def tell(self, player, text, color='silver', f=True, profile='0'):
        '''Function to send a message to a player '''

        if PLUGIN['ENABLE PLUGIN ICON']:

            profile = PLUGIN['ICON PROFILE']

        if len(self.prefix) and f:

            msg = self.scs('%s <%s>%s<end>' % (self.prefix, color, text))

        else:

            msg = self.scs('<%s>%s<end>' % (color, text))

        rust.SendChatMessage(player, msg, None, profile)

    # -------------------------------------------------------------------------
    def log(self, filename, text):
        ''' Logs text into a specific file '''

        try:

            filename = 'notifier_%s_%s.txt' % (filename, self.log_date())
            sv.Log('oxide/logs/%s'.replace('\\', '') % filename, self.format(text, True))

        except: pass

    # -------------------------------------------------------------------------
    # - PLUGIN HOOKS
    def Init(self):
        ''' Hook called when the plugin initializes '''

        self.con(LINE)

        # Update System
        self.UpdateConfig()

        global MSG, PLUGIN, COLORS, CMDS, ADVERTS, RULES, TIMED
        MSG, COLORS, PLUGIN, CMDS, ADVERTS, RULES, TIMED = [self.Config[x] for x in \
        ('MESSAGES', 'COLORS', 'SETTINGS', 'COMMANDS', 'ADVERTS', 'RULES', 'SCHEDULED MESSAGES')]

        self.prefix = '<%s>%s<end>' % (COLORS['PREFIX'], PLUGIN['PREFIX']) if PLUGIN['PREFIX'] else ''
        self.players = {}
        self.connected = []
        self.lastadvert = 0
        self.timers = {}
        self.db = data.GetData(DBNAME)

        # Cache active players
        for player in self.activelist():

            self.OnPlayerInit(player, False)

        # Start Timers
        self.con('* Starting timers:')

        for i in (
            ('ADVERTS', self.adverts),
            ('MOTD', self.motd),
            ('SCHEDULED MESSAGES', self.scheduled_messages)):

            name, func = i

            # Is System Enabled?
            if PLUGIN['ENABLE ' + name]:

                a = name + ' INTERVAL'

                if a in PLUGIN:

                    mins = PLUGIN[a]
                    secs = mins * 60 if mins else 60

                    self.timers[name] = timer.Repeat(secs, 0, Action(func), self.Plugin)

                    self.con('  - Started %s timer, set to %s minute/s' % (name.title(), mins))

                else:

                    self.timers[name] = timer.Repeat(60, 0, Action(func), self.Plugin)

                    self.con('  - Starting %s timer' % name.title())

        # Commands System
        n = 0

        self.con('* Enabling commands:')

        for cmd in CMDS:

            if PLUGIN['ENABLE %s' % cmd]:

                n += 1

                if isinstance(CMDS[cmd], tuple):

                    for i in CMDS[cmd]:

                        command.AddChatCommand(i, self.Plugin, '%s_CMD' % cmd.replace(' ','_').lower())

                    self.con('  - %s (/%s)' % (cmd.title(), ', /'.join(CMDS[cmd])))

                else:

                    command.AddChatCommand(CMDS[cmd], self.Plugin, '%s_CMD' % cmd.replace(' ','_').lower())

                    self.con('  - %s (/%s)' % (cmd.title(), CMDS[cmd]))

        if not n: self.con('  - No commands are enabled')

        # Plugin Command
        command.AddChatCommand('notifier', self.Plugin, 'plugin_CMD')

        self.con(LINE)

    # -------------------------------------------------------------------------
    def Unload(self):
        ''' Hook called on plugin unload '''

        # Destroy timers
        for i in self.timers:

            self.timers[i].Destroy()

        # Save countries database
        self.save_data()

    # -------------------------------------------------------------------------
    # - PLAYER HOOKS
    def OnPlayerInit(self, player, send=True):

        # Check for a valid player connection
        if player.net and player.net.connection:

            # Cache player info
            self.cache_player(player.net.connection)

            uid = self.playerid(player)

            # Confirm player connection
            if uid not in self.connected:

                self.connected.append(uid)

            # Check for player country info, send joining messages
            if send and uid in self.db:

                self.joining_messages(player)

            else:

                self.webrequest(player, send)

    # -------------------------------------------------------------------------
    def OnPlayerDisconnected(self, player, reason):
        ''' Hook called when a player disconnects from the server '''

        uid = self.playerid(player)

        if uid in self.players:

            ply = self.players[uid]

            if uid in self.connected:

                self.connected.remove(uid)

                if PLUGIN['ENABLE LEAVE MESSAGE'] and not (PLUGIN['HIDE ADMINS'] and player.IsAdmin()):

                    reason = reason[8:] if reason.startswith('Kicked:') else reason

                    if PLUGIN['DIFFER ADMINS JOIN/LEAVE MESSAGE'] and player.IsAdmin():

                        self.say(MSG['ADMINS LEAVE MESSAGE'].format(reason=reason, **ply.__dict__), COLORS['LEAVE MESSAGE'], uid)

                    else:

                        self.say(MSG['LEAVE MESSAGE'].format(reason=reason, **ply.__dict__), COLORS['LEAVE MESSAGE'], uid)

                self.log('connections', '{username} disconnected from {country} [UID: {steamid}][IP: {ip}]'.format(**ply.__dict__))

            del self.players[uid]

    # -------------------------------------------------------------------------
    # - ENTITY HOOKS
    def OnAirdrop(self, plane, location):
        ''' Hook called whenever a Airdrop is called '''

        if PLUGIN['ANNOUNCE AIRDROPS']:

            location = str(location).replace('(', '').replace(')', '')

            self.say(MSG['AIRDROP CALLED'].format(location=location), 'silver')

    # -------------------------------------------------------------------------
    def OnEntitySpawned(self, entity):
        ''' Hook called whenever a game entity spawns '''

        if '/patrolhelicopter.prefab' in str(entity):

            if PLUGIN['ANNOUNCE PATROL HELICOPTERS']:

                self.say(MSG['HELI CALLED'], 'silver')

    # -------------------------------------------------------------------------
    # - COMMAND FUNCTIONS
    def rules_CMD(self, player, cmd, args):
        ''' Rules command function '''

        lang = self.playerlang(player, args[0] if args else None)

        if lang:

            rules = RULES[lang]

            s = {
                'EN': 'English', 'PT': 'Portuguese', 'ES': 'Spanish', 'RO': 'Romanian', 'FR': 'French', 'IT': 'Italian',
                'DK': 'Danish', 'TR': 'Turk', 'NL': 'Dutch', 'RU': 'Russian', 'UA': 'Ukrainian', 'DE': 'German', 'HU': 'Hungarian'
            }

            if rules:

                self.tell(player, '%s <%s>%s<end>:' % (self.prefix, COLORS['BOARDS TITLE'], MSG['RULES TITLE']), f=False)
                self.tell(player, LINE, f=False)

                for line in rules:

                    self.tell(player, line, 'orange', f=False)

                if lang in s:

                    self.tell(player, LINE, f=False)
                    self.tell(player, 'Language: <grey>%s<end>' % s[lang], 'silver', f=False)

            else:

                self.tell(player, MSG['NO RULES'], COLORS['white'])

    # -------------------------------------------------------------------------
    def players_list_CMD(self, player, cmd, args):
        ''' Players List command function '''

        active = [i for i in self.activelist() if not (PLUGIN['HIDE ADMINS'] and i.IsAdmin()) or player.IsAdmin()]

        title = '%s <%s>%s<end>:' % (self.prefix, COLORS['BOARDS TITLE'], MSG['PLAYERS LIST TITLE'])

        if PLUGIN['PLAYERS LIST ON CHAT']:

            names = []

            for i in active:

                uid = self.playerid(i)

                if uid in self.players:

                    names.append('<lime>%s<end>' % self.players[uid].username)

            names = [names[i:i+3] for i in xrange(0, len(names), 3)]

            self.tell(player, title, f=False)
            self.tell(player, LINE, f=False)

            for i in names:
            
                self.tell(player, ', '.join(i), COLORS['SYSTEM'], f=False)

        if PLUGIN['PLAYERS LIST ON CONSOLE']:

            self.tell(player, LINE, f=False)
            self.tell(player, '(%s)' % MSG['CHECK CONSOLE'], 'orange', f=False)

            for i in (LINE, title, LINE):

                self.pcon(player, i)

            for num, ply in enumerate(active):

                uid = self.playerid(ply)

                if uid in self.players:

                    self.pcon(player, '<orange>{num}<end> | {steamid} | {code} | <lime>{username}<end>'.format(
                        num='%03d' % (num + 1),
                        **self.players[uid].__dict__
                    ), 'white')

            self.pcon(player, LINE)
            self.pcon(player, MSG['PLAYERS ONLINE'].format(active=str(len(active)), maxplayers=sv.maxplayers), 'orange')
            self.pcon(player, LINE)

    # -------------------------------------------------------------------------
    def players_online_CMD(self, player, cmd, args):
        ''' Player Online command function '''

        active = len(self.activelist())
        admins = len([i for i in self.activelist() if i.IsAdmin()])
        sleepers = len(self.sleeperlist())

        a = active - admins if not player.IsAdmin() and PLUGIN['HIDE ADMINS'] else active

        self.tell(player, '%s <%s>%s<end>:' % (self.prefix, COLORS['BOARDS TITLE'], MSG['PLAYERS ONLINE TITLE']), f=False)
        self.tell(player, LINE, f=False)
        self.tell(player, MSG['PLAYERS ONLINE'].format(active=str(a), maxplayers=str(sv.maxplayers)), f=False)

        if player.IsAdmin() or not PLUGIN['HIDE ADMINS']:

            self.tell(player, MSG['ADMINS ONLINE'].format(admins=str(admins)), f=False)

        self.tell(player, MSG['PLAYERS STATS'].format(sleepers=str(sleepers), alltime=str(active + sleepers)), f=False)

    # -------------------------------------------------------------------------
    def admins_list_CMD(self, player, cmd, args):
        ''' Admins List command function '''

        names = [self.players[self.playerid(i)].username for i in self.activelist() if i.IsAdmin()]
        names = [names[i:i+3] for i in xrange(0, len(names), 3)]

        if names and not PLUGIN['HIDE ADMINS'] or player.IsAdmin():

            self.tell(player, '%s <%s>%s<end>:' % (self.prefix, COLORS['BOARDS TITLE'], MSG['ADMINS LIST TITLE']), f=False)
            self.tell(player, LINE, f=False)

            for i in names:

                self.tell(player, ', '.join(i), 'white', f=False)

        else:

            self.tell(player, MSG['NO ADMINS'], COLORS['SYSTEM'])

    # -------------------------------------------------------------------------
    def plugins_list_CMD(self, player, cmd, args):
        ''' Plugins List command function '''

        self.tell(player, '%s <%s>%s<end>:' % (self.prefix, COLORS['BOARDS TITLE'], MSG['PLUGINS LIST TITLE']), f=False)
        self.tell(player, LINE, f=False)

        for i in plugins.GetAll():

            if i.Author != 'Oxide Team':
                
                self.tell(player, '<lime>{p.Title}<end> <grey>v{p.Version}<end> by {p.Author}'.format(p=i), f=False)

    # -------------------------------------------------------------------------
    def map_link_CMD(self, player, cmd, args):
        ''' Server Map command function '''

        self.tell(player, MSG['MAP LINK'].format(ip=str(sv.ip), port=str(sv.port)))

    # -------------------------------------------------------------------------
    def motd_CMD(self, player, cmd, args):
        ''' MOTD command function '''

        if not args:

            self.motd(player)

        elif player.IsAdmin():

            self.Config['MOTD'] = ' '.join(args)
            self.SaveConfig()
            self.motd(player)

    # -------------------------------------------------------------------------
    def adverts_command_CMD(self, player, cmd, args):
        ''' Adverts Command command function '''

        if args:

            if player.IsAdmin() and self.timers['ADVERTS']:

                try:

                    n = int(args[0])

                    if n:

                        self.timers['ADVERTS'].Destroy()
                        self.timers['ADVERTS'] = timer.Repeat(n * 60, 0, Action(self.adverts), self.Plugin)

                        PLUGIN['ADVERTS INTERVAL'] = n

                        self.SaveConfig()

                        self.tell(player, MSG['ADVERTS INTERVAL CHANGED'].format(minutes=str(n)), COLORS['SYSTEM'])

                except:

                    self.tell(player, MSG['SYNTAX ERROR'].format(syntax='/adverts <minutes> (i.g /adverts 5)'), 'red')

        else:

            self.tell(player, MSG['SYNTAX ERROR'].format(syntax='/adverts <minutes> (i.g /adverts 5)'), 'red')

    # -------------------------------------------------------------------------
    def plugin_CMD(self, player, cmd, args):
        ''' Plugin command function '''

        if args and args[0] == 'help':

            self.tell(player, '%sCOMMANDS DESCRIPTION:' % ('%s ' % self.prefix), f=False)
            self.tell(player, LINE, f=False)

            for cmd in CMDS:

                i = '%s DESC' % cmd

                if i in MSG:

                    self.tell(player, MSG[i], f=False)

        else:

            self.tell(player, '<#00EEEE><size=18>NOTIFIER</size> <grey>v%s<end><end>' % self.Version, f=False)
            self.tell(player, self.Description, f=False)
            self.tell(player, 'Plugin developed by <#9810FF>SkinN<end>, powered by <orange>Oxide 2<end>.', profile='76561197999302614', f=False)

    # -------------------------------------------------------------------------
    # - PLUGIN FUNCTIONS / HOOKS
    def adverts(self):
        ''' Function to send adverts to chat '''

        if ADVERTS:

            index = self.lastadvert

            if len(ADVERTS) > 1:

                while index == self.lastadvert:

                    index = random.Range(0, len(ADVERTS))

                self.lastadvert = index

            try:

                self.say(self.name_formats(ADVERTS[index]), COLORS['ADVERTS'])

            except:

                self.con('Error, Unknown name format on advert message! (Message: %s)' % ADVERTS[index])

        else:

            self.con('The Adverts list is empty, stopping Adverts loop')

            self.timers['ADVERTS'].Destroy()

    # -------------------------------------------------------------------------
    def scheduled_messages(self):
        ''' Function which broadcasts scheduled messages to chat '''

        msg = False
        now = time.strftime('%H:%M')

        if now in TIMED:

            msg = TIMED[now]

        if msg:

            if isinstance(msg, str):

                msg = (msg,)

            for i in msg:

                self.say(self.name_formats(i), COLORS['SCHEDULED MESSAGES'])

    # -------------------------------------------------------------------------
    def motd(self, player=False):

        MOTD = self.Config['MOTD']
        prefix = self.prefix
        self.prefix = PLUGIN['MOTD PREFIX']

        if player:

            self.tell(player, self.name_formats(MOTD), COLORS['MOTD'])

        else:

            self.say(self.name_formats(MOTD), COLORS['MOTD'])

        self.prefix = prefix

    # -------------------------------------------------------------------------
    def joining_messages(self, player):
        '''Sends Join and Welcome messages the when a player joins'''

        # Player UID and data
        uid = self.playerid(player)
        ply = self.players[uid]

        # Join Message
        if PLUGIN['ENABLE JOIN MESSAGE'] and not (PLUGIN['HIDE ADMINS'] and player.IsAdmin()):

            if PLUGIN['DIFFER ADMINS JOIN/LEAVE MESSAGE'] and player.IsAdmin():

                self.say(MSG['ADMINS JOIN MESSAGE'].format(**ply.__dict__), COLORS['JOIN MESSAGE'], uid)

            else:

                self.say(MSG['JOIN MESSAGE'].format(**ply.__dict__), COLORS['JOIN MESSAGE'], uid)

        # Log connection to file
        self.log('connections', '{username} connected from {country} [UID: {steamid}][IP: {ip}]'.format(**ply.__dict__))

        # Welcome Message
        if PLUGIN['ENABLE WELCOME MESSAGE']:

            lines = self.Config['WELCOME MESSAGE']

            if lines:

                self.tell(player, '\n'*50, f=False)

                for line in lines:

                    line = line.format(server=sv, **ply.__dict__)

                    self.tell(player, line, COLORS['WELCOME MESSAGE'], f=False)

            else:

                PLUGIN['ENABLE WELCOME MESSAGE'] = False

                self.con('No lines found on Welcome Message, turning it off')

    # -------------------------------------------------------------------------
    def name_formats(self, msg):
        ''' Function to format name formats on advert messages '''

        return msg.format(
            players=len(self.activelist()),
            sleepers=len(self.sleeperlist()),
            localtime=time.strftime('%H:%M'),
            localdate=time.strftime('%m/%d/%Y'),
            gametime=' '.join(str(TOD_Sky.Instance.Cycle.DateTime).split()[1:]),
            gamedate=str(TOD_Sky.Instance.Cycle.DateTime).split()[0],
            server=sv
        )

    # -------------------------------------------------------------------------
    def playerid(self, player):
        ''' Function to return the player UserID '''

        return rust.UserIDFromPlayer(player)

    # -------------------------------------------------------------------------
    def activelist(self):
        ''' Returns the active players list '''

        return BasePlayer.activePlayerList

    # -------------------------------------------------------------------------
    def sleeperlist(self):
        ''' Returns the sleepers list '''

        return BasePlayer.sleepingPlayerList

    # -------------------------------------------------------------------------
    def playername(self, con):
        '''
            Returns the player name with player or Admin default name color
        '''

        if PLUGIN['ENABLE PLAYERS DEFAULT COLORS']:

            if int(con.authLevel) > 0 and not PLUGIN['HIDE ADMINS']:

                return '<#ADFF64>%s<end>' % con.username

            else:

                return '<#6496E1>%s<end>' % con.username

        else:

            return con.username

    # -------------------------------------------------------------------------
    def playerlang(self, player, f=None):
        ''' Rules language filter '''

        default = PLUGIN['RULES LANGUAGE']

        if f:

            if f.upper() in RULES:

                return f.upper()

            else:

                self.tell(player, MSG['NO LANG'].replace('{args}', f), COLORS['SYSTEM'])

                return False

        elif default == 'AUTO':

            lang = self.players[self.playerid(player)].code

            a = {'PT': ('PT','BR'), 'ES': ('ES','MX','AR'), 'FR': ('FR','BE','CH','MC','MU')}

            for i in a:

                if lang in a[i]: lang = i

            return lang if lang in RULES else 'EN'

        else:

            return default if default in RULES else 'EN'

    # -------------------------------------------------------------------------
    def scs(self, text, con=False):
        '''
            Replaces color names and RGB hex code into HTML code
        '''

        colors = (
            'red', 'blue', 'green', 'yellow', 'white', 'black', 'cyan',
            'lightblue', 'lime', 'purple', 'darkblue', 'magenta', 'brown',
            'orange', 'olive', 'gray', 'grey', 'silver', 'maroon'
        )

        name = r'\<(\w+)\>'
        hexcode = r'\<(#\w+)\>'
        end = 'end'

        if con:
            for x in (end, name, hexcode):
                for c in re.findall(x, text):
                    if c.startswith('#') or c in colors or x == end:
                        text = text.replace('<%s>' % c, '')
        else:
            text = text.replace('<%s>' % end, '</color>')
            for f in (name, hexcode):
                for c in re.findall(f, text):
                    if c.startswith('#') or c in colors:
                        text = text.replace('<%s>' % c, '<color=%s>' % c)
        return text

    # -------------------------------------------------------------------------
    def log_date(self):
        ''' Get current date string for logging '''

        localtime = time.localtime()

        return '%02d-%s' % (localtime[1], localtime[0])

    # -------------------------------------------------------------------------
    def cache_player(self, con):
        ''' Caches player information '''

        if con:

            db = self.db
            uid = rust.UserIDFromConnection(con)
            name = self.playername(con)

            class user:

                def __init__(self):

                    self.username = name
                    self.steamid = uid
                    self.ip = con.ipaddress

                    if uid in db:

                        self.country = db[uid]['country']
                        self.code = db[uid]['code']

                    else:

                        self.country = 'Unknown'
                        self.code = 'Unknown'

            self.players[uid] = user()

    # -------------------------------------------------------------------------
    def webrequest(self, player, send=True):
        ''' Web-request filter to get the players country info '''

        uid = self.playerid(player)
        
        if uid in self.players:

            ply = self.players[uid]

            # Function to handle the web-request response
            def response_handler(code, resp):

                try:

                    # Check for any web errors
                    if resp != None or code == 200:

                        # Check if user is not in countries database
                        if uid not in self.db:

                            ply.country, ply.code = re.findall(r':\"(.+?)\"', resp)

                            self.players[uid] = ply
                            self.db[uid] = {'country': ply.country, 'code': ply.code}

                except: pass

                if send: self.joining_messages(player)

            # Call for web-request
            webrequests.EnqueueGet('http://ip-api.com/json/%s?fields=3' % ply.ip.split(':')[0], Action[Int32,String](response_handler), self.Plugin)
