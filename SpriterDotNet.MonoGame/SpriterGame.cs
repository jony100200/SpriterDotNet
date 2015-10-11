﻿// Copyright (c) 2015 The original author or authors
//
// This software may be modified and distributed under the terms
// of the zlib license.  See the LICENSE file for details.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SpriterDotNet.MonoGame
{
    public class SpriteGame : Game
    {
        private static readonly string RootDirectory = "Content";
        private static readonly string FontName = "Status";

        private static readonly IDictionary<string, string> Scmls = new Dictionary<string, string>
        {
            { "Content/GreyGuyPlus/player_006.scml", "GreyGuyPlus"},
            { "Content/GreyGuy/player.scml", "GreyGuy"},
            { "Content/TestSquares/squares.scml", "TestSquares"}
        };

        private static readonly int Width = 1280;
        private static readonly int Height = 960;
        private static readonly float MaxSpeed = 5.0f;
        private static readonly float DeltaSpeed = 0.2f;
        private static readonly string Instructions = "Enter = Next Scml\nSpace = Next Animation\nP = Anim Speed +\nO = Anim Speed -\nR = Reverse Direction\nX = Reset Animation\nT = Transition to Next Animation";

        private IList<MonogameSpriterAnimator> animators = new List<MonogameSpriterAnimator>();
        private MonogameSpriterAnimator currentAnimator;
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private SpriteFont spriteFont;
        private KeyboardState oldState;
        private string status;
        private string varValues;
        private Fps fps = new Fps();

        public SpriteGame()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = Width;
            graphics.PreferredBackBufferHeight = Height;
            Content.RootDirectory = RootDirectory;
        }

        protected override void Initialize()
        {
            base.Initialize();
            oldState = Keyboard.GetState();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            spriteBatch = new SpriteBatch(GraphicsDevice);

            spriteFont = Content.Load<SpriteFont>(FontName);
            Vector2 charPosition = new Vector2(Width / 2.0f, Height / 1.5f);

            foreach (var pair in Scmls)
            {
                string scmlPath = pair.Key;
                string spriterName = pair.Value;
                string data = File.ReadAllText(scmlPath);
                Spriter spriter = SpriterParser.Parse(data);

                foreach(SpriterEntity entity in spriter.Entities)
                {
                    var animator = new MonogameSpriterAnimator(spriter, entity, charPosition, spriteBatch, GraphicsDevice);
                    RegisterTextures(animator, spriter, spriterName);
                    animators.Add(animator);
                }
            }

            currentAnimator = animators.First();
            currentAnimator.AnimationFinished += OnAnimationFinished;
        }

        private void OnAnimationFinished(string name)
        {
            Console.WriteLine(name);
        }

        protected override void Draw(GameTime gameTime)
        {
            fps.OnDraw(gameTime);

            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin(SpriteSortMode.Deferred);

            DrawText(String.Format("FPS (Update) = {0}\nFPS (Draw) =    {1}", fps.UpdateFps, fps.DrawFps), new Vector2(Width - 200, 10), 0.6f);
            DrawText(Instructions, new Vector2(10, 10), 0.6f);
            DrawText(status, new Vector2(10, Height - 50));
            DrawText(varValues, new Vector2(Width - 300, Height - 200), 0.6f);
            currentAnimator.Step((float)gameTime.ElapsedGameTime.Milliseconds);

            spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawText(string text, Vector2 position, float size = 1.0f)
        {
            spriteBatch.DrawString(spriteFont, text, position, Color.Black, 0, Vector2.Zero, size, SpriteEffects.None, 0.0f);
        }

        protected override void Update(GameTime gameTime)
        {
            fps.OnUpdate(gameTime);

            if (IsPressed(Keys.Enter)) SwitchScml();
            if (IsPressed(Keys.Space)) currentAnimator.Play(GetNextAnimation());
            if (IsPressed(Keys.P)) ChangeAnimationSpeed(DeltaSpeed);
            if (IsPressed(Keys.O)) ChangeAnimationSpeed(-DeltaSpeed);
            if (IsPressed(Keys.R)) currentAnimator.Speed = -currentAnimator.Speed;
            if (IsPressed(Keys.X)) currentAnimator.Play(currentAnimator.Name);
            if (IsPressed(Keys.T)) currentAnimator.Transition(GetNextAnimation(), 1000.0f);

            oldState = Keyboard.GetState();

            string entity = currentAnimator.Entity.Name;
            status = String.Format("{0} : {1}", entity, currentAnimator.Name);
            varValues = GetVarValues();

            base.Update(gameTime);
        }

        private string GetVarValues()
        {
            StringBuilder sb = new StringBuilder();
            foreach(string name in currentAnimator.GetVarNames())
            {
                object value;
                SpriterVarValue sValue = currentAnimator.GetVarValue(name);
                switch (sValue.Type)
                {
                    case SpriterVarType.Float:
                        value = sValue.FloatValue;
                        break;
                    case SpriterVarType.Int:
                        value = sValue.IntValue;
                        break;
                    default:
                        value = sValue.StringValue;
                        break;
                }
                sb.Append(name).Append(" = ").Append(value).Append("\n");
            }
            return sb.ToString();
        }

        private bool IsPressed(Keys key)
        {
            KeyboardState state = Keyboard.GetState();
            return oldState.IsKeyUp(key) && state.IsKeyDown(key);
        }

        private void SwitchScml()
        {
            int index = animators.IndexOf(currentAnimator);
            ++index;
            if (index >= animators.Count) index = 0;
            currentAnimator = animators[index];
        }

        private string GetNextAnimation()
        {
            List<string> animations = currentAnimator.GetAnimations().ToList();
            int index = animations.IndexOf(currentAnimator.CurrentAnimation.Name);
            ++index;
            if (index >= animations.Count) index = 0;
            return animations[index];
        }

        private void ChangeAnimationSpeed(float delta)
        {
            var speed = currentAnimator.Speed + delta;
            speed = Math.Abs(speed) < MaxSpeed ? speed : MaxSpeed * Math.Sign(speed);
            currentAnimator.Speed = speed;
        }

        private void RegisterTextures(MonogameSpriterAnimator animator, Spriter spriter, string spriterName)
        {
            foreach (SpriterFolder folder in spriter.Folders)
            {
                foreach (SpriterFile file in folder.Files)
                {
                    if (file.Type != SpriterFileType.Image) continue;
                    string path = FormatPath(folder, file, spriterName);
                    Texture2D texture = null;
                    try
                    {
                        texture = Content.Load<Texture2D>(path);
                    }
                    catch
                    {
                        Debug.WriteLine("Missing Texture: " + path);
                    }
                    if (texture == null) continue;

                    animator.Register(folder.Id, file.Id, texture);
                }
            }
        }

        private string FormatPath(SpriterFolder folder, SpriterFile file, string spriterName)
        {
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            if (String.IsNullOrEmpty(folder.Name)) return String.Format("{0}/{1}", spriterName, fileName);
            return String.Format("{0}/{1}/{2}", spriterName, folder.Name, fileName);
        }
    }
}