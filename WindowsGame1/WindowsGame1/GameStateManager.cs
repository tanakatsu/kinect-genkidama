using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;


namespace WindowsGame1
{
    enum GameState { Ready, Playing };

    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class GameStateManager : Microsoft.Xna.Framework.GameComponent
    {
        Game1 game;
        SpriteFont fontArial = null; //フォント
        public int gameState; // 状態

        public GameStateManager(Game1 game)
            : base(game)
        {
            // TODO: Construct any child components here
            this.game = game;
            this.gameState = (int)GameState.Ready;
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            // TODO: Add your initialization code here

            // フォントの読み込み
            this.fontArial = this.game.Content.Load<SpriteFont>("Arial"); // ContentにArial.spritefontを追加しておく

            base.Initialize();
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here

            switch (gameState)
            {
                case (int)GameState.Ready:
                    if (this.game.gestureDetect.gesture_ok)
                    {
                        gameState = (int)GameState.Playing;
                    }
                    break;

                case (int)GameState.Playing:
                    if (this.game.gestureDetect.gesture_byebye)
                    {
                        gameState = (int)GameState.Ready;
                    }
                    break;
            }

            base.Update(gameTime);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            string infoText = "";

            // 現在の状態を表示 (デバッグ用）
            switch(gameState)
            {
                case (int)GameState.Ready:
                    infoText = "Ready";
                    break;
                case (int)GameState.Playing:
                    infoText = "Playing";
                    break;
            }

            spriteBatch.Begin();
            spriteBatch.DrawString(fontArial, infoText, new Vector2(0, 0), Color.White);
            spriteBatch.End();
        }
    }
}
