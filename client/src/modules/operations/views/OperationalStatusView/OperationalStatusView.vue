<script lang="ts" src="./OperationalStatusView.ts"></script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-header__title">{{ t('status.title') }}</h1>
        <p class="page-header__subtitle">{{ t('status.subtitle') }}</p>
      </div>

      <div class="inline-actions">
        <el-button :loading="operationsStore.isLoading" @click="refresh">
          {{ t('status.refresh') }}
        </el-button>
      </div>
    </div>

    <el-alert
      v-if="operationsStore.hasLoadFailure"
      class="status-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <el-row :gutter="16">
      <el-col :xs="24" :lg="12">
        <div class="panel status-panel">
          <span class="panel-title">{{ t('status.killSwitch') }}</span>

          <el-tag :type="killSwitchTagType" effect="dark" disable-transitions>
            {{ t(killSwitchStateKey) }}
          </el-tag>

          <MetricRow
            label-key="status.reason"
            :value-key="hasKillSwitchReason ? '' : 'status.never'"
            :value="killSwitch?.reason ?? ''"
            :muted="!hasKillSwitchReason"
          />

          <MetricRow
            label-key="status.changedAt"
            :value="formatTimestamp(killSwitch?.changedAtUtc ?? null)"
            :muted="!killSwitch?.changedAtUtc"
          />

          <div class="inline-actions status-panel__actions">
            <el-input
              v-model="engageReason"
              class="status-panel__reason"
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
      </el-col>

      <el-col :xs="24" :lg="12">
        <div class="panel status-panel">
          <span class="panel-title">{{ t('status.account') }}</span>

          <template v-if="account">
            <MetricRow
              label-key="status.brokerAccountId"
              :value="String(account.brokerAccountId)"
            />
            <MetricRow label-key="status.environment" :value-key="environmentKey" />
            <MetricRow label-key="status.connectionState" :value-key="connectionKey" />
            <MetricRow
              label-key="status.tradingEnabled"
              :value-key="account.isTradingEnabled ? 'common.yes' : 'common.no'"
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
          <span class="panel-title">{{ t('status.marketManager') }}</span>

          <template v-if="marketManager">
            <MetricRow label-key="status.mode" :value-key="modeKey" />
            <MetricRow label-key="status.continuousAnalysis" :value-key="analysisKey" />
          </template>

          <p v-else class="status-panel__empty">{{ t('status.never') }}</p>
        </div>

        <div class="panel status-panel">
          <span class="panel-title">{{ t('status.evidenceStore') }}</span>

          <template v-if="evidenceStore">
            <MetricRow label-key="status.evidenceProvider" :value="evidenceStore.provider" />
            <MetricRow
              label-key="status.evidenceAvailable"
              :value-key="evidenceStore.isAvailable ? 'common.yes' : 'common.no'"
            />
            <template v-if="embedding">
              <MetricRow label-key="status.embeddingEngine" :value="embedding.engine" />
              <MetricRow
                label-key="status.embeddingModel"
                :value="embedding.model || t('status.never')"
              />
              <MetricRow
                label-key="status.embeddingTextVersion"
                :value="embedding.textVersion || t('status.never')"
              />
              <MetricRow
                label-key="status.embeddingAvailable"
                :value-key="embedding.isAvailable ? 'common.yes' : 'common.no'"
              />
            </template>
          </template>

          <p v-else class="status-panel__empty">{{ t('status.never') }}</p>
        </div>

        <div class="panel status-panel">
          <span class="panel-title">{{ t('status.analysisModel') }}</span>

          <template v-if="analysisModel">
            <MetricRow label-key="status.evidenceProvider" :value="analysisModel.provider" />
            <MetricRow
              label-key="status.analysisModelName"
              :value="analysisModel.model || t('status.never')"
            />
            <MetricRow
              label-key="status.evidenceAvailable"
              :value-key="analysisModel.isAvailable ? 'common.yes' : 'common.no'"
            />
          </template>

          <p v-else class="status-panel__empty">{{ t('status.never') }}</p>
        </div>
      </el-col>
    </el-row>

    <div class="panel status-panel">
      <span class="panel-title">{{ t('status.serverTime') }}</span>
      <MetricRow
        label-key="status.serverTime"
        :value="formatTimestamp(status?.serverTimeUtc ?? null)"
      />
    </div>
  </div>
</template>

<style scoped lang="less" src="./OperationalStatusView.less"></style>
