<script lang="ts" src="./OperationalStatusView.ts"></script>

<template>
  <div class="cockpit">
    <header class="page-header cockpit__header">
      <div>
        <div class="cockpit__eyebrow">{{ t('status.liveSnapshot') }}</div>
        <h1 class="page-header__title">{{ t('status.title') }}</h1>
        <p class="page-header__subtitle">{{ t('status.subtitle') }}</p>
      </div>

      <div class="cockpit__header-actions">
        <span class="cockpit__updated"
          >{{ t('status.snapshotUpdated') }}
          {{ formatTimestamp(status?.serverTimeUtc ?? null) }}</span
        >
        <el-button :loading="operationsStore.isLoading" @click="refresh">
          {{ t('status.refresh') }}
        </el-button>
      </div>
    </header>

    <el-alert
      v-if="operationsStore.hasLoadFailure"
      class="status-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <section class="cockpit__hero-grid">
      <div class="panel cockpit-hero">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.systemHealth') }}</span>
          <el-tag :type="systemTagType" effect="light" disable-transitions>
            {{ t(systemStateKey) }}
          </el-tag>
        </div>

        <div class="cockpit-hero__main">
          <div>
            <div class="cockpit-hero__title">{{ t('status.allOperational') }}</div>
            <p class="cockpit-hero__copy">{{ t('status.riskSummaryHint') }}</p>
          </div>
          <div class="cockpit-hero__signal" :class="`is-${systemTagType}`">
            <span></span>
            <strong>{{ t(systemStateKey) }}</strong>
          </div>
        </div>

        <div class="cockpit-hero__checks">
          <div class="cockpit-check">
            <span class="cockpit-check__label">{{ t('status.account') }}</span>
            <strong>{{ account ? t(connectionKey) : t('status.noAccount') }}</strong>
          </div>
          <div class="cockpit-check">
            <span class="cockpit-check__label">{{ t('status.marketManager') }}</span>
            <strong>{{ marketManager ? t(analysisKey) : t('status.never') }}</strong>
          </div>
          <div class="cockpit-check">
            <span class="cockpit-check__label">{{ t('status.modelStatus') }}</span>
            <strong>{{
              analysisModel?.isAvailable ? t('status.enabled') : t('status.disabled')
            }}</strong>
          </div>
        </div>
      </div>

      <div class="panel cockpit-attention">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.securitySummary') }}</span>
          <el-tag :type="killSwitchTagType" effect="light" disable-transitions>
            {{ t(killSwitchStateKey) }}
          </el-tag>
        </div>
        <div class="cockpit-attention__state">
          <span class="cockpit-attention__dot" :class="{ 'is-danger': isKillSwitchEngaged }"></span>
          <div>
            <strong>{{ t(tradingStateKey) }}</strong>
            <p>{{ hasKillSwitchReason ? killSwitch?.reason : t('status.killSwitchReady') }}</p>
          </div>
        </div>
        <div class="cockpit-attention__actions">
          <el-input
            v-model="engageReason"
            :placeholder="t('status.engageReason')"
            :disabled="isKillSwitchEngaged"
          />
          <el-button type="danger" :disabled="isKillSwitchEngaged" @click="engage">
            {{ t('status.engage') }}
          </el-button>
          <el-button type="success" :disabled="!isKillSwitchEngaged" @click="release">
            {{ t('status.release') }}
          </el-button>
        </div>
      </div>
    </section>

    <section class="cockpit__metrics">
      <div class="panel cockpit-metric">
        <span class="cockpit-metric__label">{{ t('status.accountSummary') }}</span>
        <strong class="cockpit-metric__value">{{ account ? account.brokerAccountId : '—' }}</strong>
        <span class="cockpit-metric__hint">{{
          account ? t(environmentKey) : t('status.noAccount')
        }}</span>
      </div>
      <div class="panel cockpit-metric">
        <span class="cockpit-metric__label">{{ t('status.connectionState') }}</span>
        <strong class="cockpit-metric__value cockpit-metric__value--compact">
          {{ account ? t(connectionKey) : t('status.never') }}
        </strong>
        <span class="cockpit-metric__hint">{{
          formatTimestamp(account?.lastBrokerSyncUtc ?? null)
        }}</span>
      </div>
      <div class="panel cockpit-metric">
        <span class="cockpit-metric__label">{{ t('status.analysisStatus') }}</span>
        <strong class="cockpit-metric__value cockpit-metric__value--compact">{{
          t(analysisStateKey)
        }}</strong>
        <span class="cockpit-metric__hint">{{
          formatTimestamp(marketManager?.lastCycleAtUtc ?? null)
        }}</span>
      </div>
      <div class="panel cockpit-metric">
        <span class="cockpit-metric__label">{{ t('status.modelStatus') }}</span>
        <strong class="cockpit-metric__value cockpit-metric__value--compact">
          {{ analysisModel?.model || t('status.never') }}
        </strong>
        <span class="cockpit-metric__hint">{{ analysisModel?.provider || t('status.never') }}</span>
      </div>
    </section>

    <section class="cockpit__lower-grid">
      <div class="panel status-panel">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.account') }}</span>
          <el-tag v-if="account" :type="connectionTagType" effect="light" disable-transitions>
            {{ t(connectionKey) }}
          </el-tag>
        </div>

        <template v-if="account">
          <MetricRow label-key="status.brokerAccountId" :value="String(account.brokerAccountId)" />
          <MetricRow label-key="status.environment" :value-key="environmentKey" />
          <MetricRow
            label-key="status.tradingEnabled"
            :value-key="account.isTradingEnabled ? 'status.enabled' : 'status.disabled'"
          />
          <MetricRow
            label-key="status.lastBrokerSync"
            :value="formatTimestamp(account.lastBrokerSyncUtc)"
            :muted="!account.lastBrokerSyncUtc"
          />
          <MetricRow
            label-key="status.lastReconciled"
            :value="formatTimestamp(account.lastReconciledUtc)"
            :muted="!account.lastReconciledUtc"
          />
        </template>
        <p v-else class="status-panel__empty">{{ t('status.noAccount') }}</p>
      </div>

      <div class="panel status-panel">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.riskSummary') }}</span>
          <span class="cockpit-panel-mark">✓</span>
        </div>
        <div class="cockpit-risk-row">
          <span>{{ t('status.killSwitch') }}</span
          ><strong>{{ t(killSwitchStateKey) }}</strong>
        </div>
        <div class="cockpit-risk-row">
          <span>{{ t('status.continuousAnalysis') }}</span
          ><strong>{{ t(analysisStateKey) }}</strong>
        </div>
        <div class="cockpit-risk-row">
          <span>{{ t('status.semanticMemory') }}</span
          ><strong>{{ t(evidenceStateKey) }}</strong>
        </div>
        <div class="cockpit-risk-row">
          <span>{{ t('status.serverTime') }}</span
          ><strong>{{ formatTimestamp(status?.serverTimeUtc ?? null) }}</strong>
        </div>
      </div>

      <div class="panel status-panel">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.marketManager') }}</span>
          <el-tag
            v-if="marketManager"
            :type="marketManager.isAnalysisRunning ? 'success' : 'warning'"
            effect="light"
            disable-transitions
          >
            {{ t(analysisKey) }}
          </el-tag>
        </div>
        <template v-if="marketManager">
          <MetricRow label-key="status.mode" :value-key="modeKey" />
          <MetricRow
            label-key="status.lastCycleAt"
            :value="formatTimestamp(marketManager.lastCycleAtUtc)"
            :muted="!marketManager.lastCycleAtUtc"
          />
        </template>
        <p v-else class="status-panel__empty">{{ t('status.never') }}</p>
      </div>
    </section>

    <section class="cockpit__technical">
      <div class="panel status-panel">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.semanticMemory') }}</span>
          <span class="cockpit-panel-mark" :class="{ 'is-warning': !evidenceStore?.isAvailable }">{{
            evidenceStore?.isAvailable ? '✓' : '!'
          }}</span>
        </div>
        <template v-if="evidenceStore">
          <MetricRow label-key="status.evidenceProvider" :value="evidenceStore.provider" />
          <MetricRow
            label-key="status.evidenceAvailable"
            :value-key="evidenceStore.isAvailable ? 'status.enabled' : 'status.disabled'"
          />
          <MetricRow
            v-if="embedding"
            label-key="status.embeddingEngine"
            :value="embedding.engine"
          />
          <MetricRow
            v-if="embedding"
            label-key="status.embeddingModel"
            :value="embedding.model || t('status.never')"
          />
        </template>
        <p v-else class="status-panel__empty">{{ t('status.never') }}</p>
      </div>

      <div class="panel status-panel">
        <div class="panel-header">
          <span class="panel-title">{{ t('status.analysisModel') }}</span>
          <span class="cockpit-panel-mark" :class="{ 'is-warning': !analysisModel?.isAvailable }">{{
            analysisModel?.isAvailable ? '✓' : '!'
          }}</span>
        </div>
        <template v-if="analysisModel">
          <MetricRow label-key="status.evidenceProvider" :value="analysisModel.provider" />
          <MetricRow
            label-key="status.analysisModelName"
            :value="analysisModel.model || t('status.never')"
          />
          <MetricRow
            label-key="status.evidenceAvailable"
            :value-key="analysisModel.isAvailable ? 'status.enabled' : 'status.disabled'"
          />
        </template>
        <p v-else class="status-panel__empty">{{ t('status.never') }}</p>
      </div>
    </section>
  </div>
</template>

<style scoped lang="less" src="./OperationalStatusView.less"></style>
