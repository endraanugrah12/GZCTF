import { Button, Group, NumberInput, Slider, Stack, Text, useMantineColorScheme, useMantineTheme } from '@mantine/core'
import type { EChartsOption } from 'echarts'
import { FC, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { EchartsContainer } from '@Components/charts/EchartsContainer'
import { challengeScore } from '@Utils/ChallengeScoring'
import { ScoreCurve } from '@Api'

interface ScoreFuncProps {
  originalScore: number
  difficulty: number
  minScoreRate: number
  currentAcceptCount: number
  curve?: ScoreCurve
}

export const ScoreFunc: FC<ScoreFuncProps> = ({
  originalScore,
  difficulty,
  minScoreRate,
  currentAcceptCount,
  curve = ScoreCurve.Standard,
}) => {
  const [possibleSolves, setPossibleSolves] = useState(Math.max(10, currentAcceptCount))
  const [selectedSolves, setSelectedSolves] = useState(1)
  const func = (count: number) => challengeScore(originalScore, minScoreRate, difficulty, count, curve)
  const curScore = func(selectedSolves)
  const showCount = selectedSolves
  const theme = useMantineTheme()
  const samples = Math.min(200, possibleSolves)
  const plotCounts = [
    ...new Set([
      ...Array.from({ length: samples + 1 }, (_, n) => Math.round((n * possibleSolves) / samples)),
      selectedSolves,
    ]),
  ].sort((a, b) => a - b)
  const plotData = plotCounts.map((count) => [count, func(count)])
  const { colorScheme } = useMantineColorScheme()
  const { t } = useTranslation()
  const primaryColors = theme.colors[theme.primaryColor]
  const color = primaryColors[colorScheme === 'dark' ? 8 : 6]

  const option: EChartsOption = useMemo(
    () =>
      ({
        animation: false,
        backgroundColor: 'transparent',
        grid: {
          top: 30,
          left: 40,
          right: 70,
          bottom: 30,
          backgroundColor: 'transparent',
        },
        xAxis: {
          name: t('admin.content.games.challenges.solve_count'),
          min: 0,
          max: possibleSolves,
          minInterval: 1,
        },
        yAxis: {
          name: t('admin.content.games.challenges.score'),
          min: 0,
          max: Math.ceil((originalScore * 1.2) / 100) * 100,
        },
        series: [
          {
            type: 'line',
            showSymbol: false,
            clip: true,
            color: color,
            data: plotData,
            markPoint: {
              label: {
                show: true,
                fontSize: 10,
                formatter: '{c}',
              },
              symbol: 'pin',
              symbolSize: 40,
              symbolOffset: [0, 0],
              data: [
                {
                  name: t('game.label.score'),
                  value: curScore,
                  xAxis: showCount,
                  yAxis: curScore,
                },
              ],
            },
            markLine: {
              symbol: 'none',
              data: [
                {
                  yAxis: Math.floor(originalScore * minScoreRate),
                  label: {
                    position: 'end',
                    formatter: 'min: {c}',
                  },
                },
              ],
            },
          },
        ],
      }) satisfies EChartsOption,
    [theme, originalScore, difficulty, minScoreRate, selectedSolves, possibleSolves, curve, color, t]
  )

  return (
    <Stack gap="sm">
      <Text fw={600}>Score simulator</Text>
      <Text size="xs" c="dimmed">
        Form values: {curve} · Initial {originalScore} · Minimum {Math.ceil(originalScore * minScoreRate)} · Decay{' '}
        {difficulty}
      </Text>
      <Text size="xs" c="dimmed">
        Preview only, before blood bonuses. Save challenge settings to apply changes. The range below does not change
        decay.
      </Text>
      <Group align="end">
        <NumberInput
          label="Possible solves / teams"
          min={1}
          max={100000}
          allowDecimal={false}
          value={possibleSolves}
          onChange={(value) => {
            if (typeof value !== 'number' || !Number.isFinite(value)) return
            const maximum = Math.max(1, Math.min(100000, Math.floor(value)))
            setPossibleSolves(maximum)
            setSelectedSolves((count) => Math.min(count, maximum))
          }}
        />
        <Button
          variant="light"
          size="xs"
          onClick={() => {
            setPossibleSolves((maximum) => Math.max(maximum, currentAcceptCount, 1))
            setSelectedSolves(currentAcceptCount)
          }}
        >
          Use current count ({currentAcceptCount})
        </Button>
      </Group>
      <Text aria-live="polite" fw={700}>
        {selectedSolves} solves → {curScore} points
      </Text>
      <Slider
        aria-label="Simulated solve count"
        min={0}
        max={possibleSolves}
        step={1}
        value={selectedSolves}
        onChange={setSelectedSolves}
        label={(count) => `${count} solves: ${func(count)} points`}
      />
      <EchartsContainer
        option={option}
        opts={{
          renderer: 'svg',
        }}
        style={{
          width: '100%',
          height: 260,
          display: 'flex',
        }}
      />
    </Stack>
  )
}
