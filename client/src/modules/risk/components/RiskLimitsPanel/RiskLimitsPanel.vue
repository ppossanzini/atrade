<script lang="ts" src="./RiskLimitsPanel.ts"></script>

<template>
  <div class="risk-limits-panel">
    <div class="panel-header">
      <span class="panel-title">{{ t('risk.limitsTitle') }}</span>

      <el-tag
        v-if="limits"
        :type="limits.isFullyConfigured ? 'success' : 'warning'"
        size="small"
        effect="plain"
      >
        {{ limits.isFullyConfigured ? t('risk.limitsComplete') : t('risk.limitsIncomplete') }}
      </el-tag>
    </div>

    <el-skeleton v-if="isLoading && !limits" :rows="4" animated />

    <p v-else-if="!limits" class="panel-empty">{{ t('risk.limitsUnavailable') }}</p>

    <template v-else>
      <div class="metric-row">
        <span class="metric-row__label">{{ t('risk.snapshotMaxAge') }}</span>
        <span
          class="metric-row__value"
          :class="{ 'metric-row__value--muted': limits.snapshotMaxAgeSeconds === null }"
        >
          {{ formatThreshold(limits.snapshotMaxAgeSeconds, 'seconds') }}
        </span>
      </div>

      <el-alert
        v-if="!limits.isFullyConfigured"
        class="panel-alert"
        type="warning"
        :closable="false"
        show-icon
        :title="t('risk.limitsIncompleteHint')"
      />

      <div v-for="market in markets" :key="market.market" class="risk-limits-panel__market">
        <div class="panel-header">
          <span class="risk-limits-panel__market-name">{{ t(`marketKind.${market.market}`) }}</span>
          <el-tag :type="market.isConfigured ? 'success' : 'warning'" size="small" effect="plain">
            {{ market.isConfigured ? t('risk.configured') : t('risk.notConfigured') }}
          </el-tag>
        </div>

        <div class="metric-row">
          <span class="metric-row__label">{{ t('risk.legSpreadMax') }}</span>
          <span
            class="metric-row__value"
            :class="{ 'metric-row__value--muted': market.legSpreadMaxPips === null }"
          >
            {{ formatThreshold(market.legSpreadMaxPips, 'pips') }}
          </span>
        </div>

        <div class="metric-row">
          <span class="metric-row__label">{{ t('risk.legVolatilityMax') }}</span>
          <span
            class="metric-row__value"
            :class="{ 'metric-row__value--muted': market.legVolatilityMaxPercent === null }"
          >
            {{ formatThreshold(market.legVolatilityMaxPercent, 'percent') }}
          </span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="less" src="./RiskLimitsPanel.less"></style>
