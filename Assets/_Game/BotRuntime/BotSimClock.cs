using System;
using UnityEngine;

namespace Game.BotRuntime
{
    /// <summary>
    /// 봇의 눈·두뇌·심판이 함께 보는 시계. 기본은 Unity 시간이지만, 봇의 몸이 Fusion 틱으로
    /// 움직이는 동안에는 Fusion 시뮬레이션 시간을 꽂아 두 시계를 하나로 맞춘다.
    ///
    /// 왜 필요한가: 학습 배율(time-scale)을 올리면 Unity 시간은 빨라지지만 Fusion은 프레임당
    /// 틱 한도 때문에 그만큼 따라오지 못한다. 심판이 Unity 시간을 보면 "30초 제한"이 봇 기준
    /// 3초가 되어 옳은 선택도 시간 초과가 된다. 심판이 봇의 시계를 보면 배율은 처리 속도만 바꾼다.
    /// </summary>
    public static class BotSimClock
    {
        private static Func<double> source;

        /// <summary>현재 시각(초). 주입된 시계가 없으면 Unity 시간.</summary>
        public static double Now => source != null ? source() : Time.timeAsDouble;

        public static bool IsInjected => source != null;

        public static void Use(Func<double> clock)
        {
            source = clock;
        }

        public static void Reset()
        {
            source = null;
        }
    }
}
